// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include "src/HeaderBlock.h"

#include "src/IndexBlock.h"
#include "src/KeyVersionBlock.h"
#include "src/StateBlock.h"
#include "src/SubkeyVersionBlock.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>
#include <Microsoft/MixedReality/Sharing/Common/hash.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/AbstractKeyWithHandle.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Behavior.h>

#include <algorithm>
#include <cassert>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace {
constexpr uint32_t GetSlotHash8FromHash(uint64_t hash) {
  return static_cast<uint8_t>(hash);
}

constexpr uint32_t GetIndexOffsetFromHash(uint64_t hash) {
  return static_cast<uint32_t>(hash >> 8);
}

#ifdef MS_MR_SHARING_PLATFORM_AMD64
struct HashMasks {
  constexpr HashMasks() noexcept : masks{} {
    for (size_t i = 0; i < 256; ++i) {
      masks[static_cast<size_t>(IndexLevel::Key)][i] =
          (~(0xFFu << IndexBlock::GetKeysCount(i))) << 1;
      masks[static_cast<size_t>(IndexLevel::Subkey)][i] =
          ~(0xFFu >> IndexBlock::GetSubkeysCount(i));
    }
  }
  // 8-bit masks to mask-out the hashes relevant to the search.
  // The array is indexed by the search kind (keys or subkeys),
  // and then by the lowest byte of IndexBlock::counts_and_hashes_,
  // which contains the information about the number of keys or subkeys.
  // For example, if we are searching keys, for all values of counts_and_hashes_
  // associated with "3 keys" the value of the mask will be 0b00001110.
  // And if we are searching subkeys, for all values of counts_and_hashes_
  // associated with "3 subkeys" it will be 0b11100000.
  //
  // See the layout of IndexBlock for additional details.
  uint8_t masks[2][256];
};
constexpr HashMasks kHashMasks;
#endif  // #ifdef MS_MR_STATESYNC_PLATFORM_AMD64
}  // namespace

inline bool HeaderBlock::IsVersionFromThisBlock(uint64_t version) const
    noexcept {
  return (base_version_ <= version) &&
         (version - base_version_ < stored_versions_count());
}

inline uint32_t HeaderBlock::available_data_blocks_count() const noexcept {
  return data_blocks_capacity_ - stored_data_blocks_count_ -
         ((stored_versions_count() + VersionRefCount::kCountsPerBlock - 1) /
          VersionRefCount::kCountsPerBlock);
}

inline DataBlockLocation HeaderBlock::AllocateDataBlock() noexcept {
  assert(available_data_blocks_count() > 0);
  return DataBlockLocation{stored_data_blocks_count_++};
}

// FIXME: this is a very slow approach; the code should be rewritten in a
// trivial non-recursive form.
class HeaderBlock::BlockInserter {
 public:
  StateBlockBase& GetAt(DataBlockLocation location) noexcept {
    return accessor_.GetBlockAt<StateBlockBase>(location);
  }

  BlockInserter(Accessor& accessor, uint64_t hash)
      : accessor_{accessor},
        new_block_data_location_{accessor.header_block().AllocateDataBlock()},
        new_block_{GetAt(new_block_data_location_)} {
    assert(accessor.header_block().remaining_index_slots_capacity_ > 0);
    --accessor.header_block().remaining_index_slots_capacity_;
    // If we won't find a free slot, we'll write a bit indicating that there was
    // a failed insertion attempt. We'll use kThisBlockOverflowMask for the
    // first block and kPrecedingBlocksOverflowMask for the next blocks (see
    // below).
    uint64_t overflow_mask = IndexBlock::kThisBlockOverflowMask;
    const auto index_blocks_mask = accessor.header_block().index_blocks_mask_;
    for (auto offset = GetIndexOffsetFromHash(hash);; ++offset) {
      uint32_t index_block_id = offset & index_blocks_mask;
      IndexBlock& index = accessor.GetIndexBlock(index_block_id);
      const auto counts_and_hashes = index.counts_and_hashes();
      if (IndexBlock::HasFreeSlots(counts_and_hashes)) {
        counts_and_hashes_ = counts_and_hashes;
        index_block_id_ = index_block_id;
        return;
      }
      index.counts_and_hashes_.store(counts_and_hashes | overflow_mask,
                                     std::memory_order_relaxed);
      overflow_mask = IndexBlock::kPrecedingBlocksOverflowMask;
    }
  }

  IndexSlotLocation index_slot_location() const noexcept {
    return index_slot_location_;
  }

 protected:
  virtual bool IsNewBlockLessThan(const StateBlockBase& other) const
      noexcept = 0;

  void PublishToSortedList(DataBlockLocation& tree_root,
                           std::atomic<IndexSlotLocation>& list_head) {
    const bool is_tree_root_valid = tree_root != DataBlockLocation::kInvalid;
    // The tree and the linked list are either both empty or not.
    assert(is_tree_root_valid == (list_head.load(std::memory_order_relaxed) !=
                                  IndexSlotLocation::kInvalid));

    std::atomic<IndexSlotLocation>* prev_next = &list_head;
    if (is_tree_root_valid) {
      Insert(tree_root);
      if (previous_block_)
        prev_next = &previous_block_->next_;
      new_block_.next_.store(prev_next->load(std::memory_order_relaxed),
                             std::memory_order_relaxed);
    } else {
      tree_root = new_block_data_location_;
    }
    // memory_order_release since we want to make sure the readers of this
    // list see all writes already made to the new block.
    prev_next->store(index_slot_location_, std::memory_order_release);
  }

  void Insert(DataBlockLocation& parent_location) noexcept {
    assert(parent_location != DataBlockLocation::kInvalid);
    auto& parent_block = GetAt(parent_location);
    // Compares keys for KeyIndexBlock or subkeys for SubkeyIndexBlock.
    // We know that there is no block that is equal to the block we are
    // inserting, so if the node is not less than the parent, it's
    // certainly greater than the parent.
    if (IsNewBlockLessThan(parent_block)) {
      if (parent_block.left_tree_child_ == DataBlockLocation::kInvalid) {
        // Left child is missing, inserting the new node there:
        //
        //  [parent]    |
        //   /    \     |
        // (*)  [right] |
        //  ^ inserting here.
        //
        // If the parent's 'level' was 0, we'll be violating the AA-tree
        // invariant (the level of the left child must be less by 1).
        // We'll fix it below.
        parent_block.left_tree_child_ = new_block_data_location_;
      } else {
        Insert(parent_block.left_tree_child_);
      }

      // Repairing the invariants of the AA-tree if necessary.
      // Since we updated the left subtree, the only invariant that can
      // be broken immediately is the one that the level of the left
      // subtree must be 1 less than the level of the parent. Repairing
      // it with a "skew" operation may break the second invariant, so
      // we'll bump the level of the parent instead (which is equivalent
      // to a skew+split pair).

      DataBlockLocation left_location = parent_block.left_tree_child_;
      assert(left_location != DataBlockLocation::kInvalid);
      auto& left_block = GetAt(left_location);
      const auto left_level = left_block.tree_level();
      const auto parent_level = parent_block.tree_level();
      if (left_level == parent_level) {
        // AA-tree invariant is broken. To repair it, we need to perform
        // the skew operation unless it breaks another invariant of the
        // AA-tree (the level of every right grandchild must be less
        // than that of its grandparent). In that case we'll just bump
        // the level of the parent instead (this is equivalent to
        // performing both the skew operation and the split operation in
        // order).
        DataBlockLocation right_location = parent_block.right_tree_child_;
        if (right_location == DataBlockLocation::kInvalid ||
            GetAt(right_location).tree_level() < parent_level) {
          // Skewing the tree:
          //   [parent]          [left]        |
          //    /  \             /   \         |
          // [left][RR]    =>   ?   [parent]   |
          //  /  \                   /    \    |
          // ? [grandchild]   [grandchild][RR] |
          //
          // This will not break the other invariant since [RR] is
          // either empty, or its level is less than the level of the
          // parent.
          parent_block.left_tree_child_ = left_block.right_tree_child_;
          left_block.right_tree_child_ = parent_location;
          parent_location = left_location;
        } else {
          // We couldn't perform the skew operation because it would break the
          // other invariant, so we just bump the level of the parent. This may
          // break some invariant for the higher level, but we'll repair it in
          // the calling code if necessary.
          parent_block.IncrementTreeLevel();
        }
      }
      return;
    }
    // The inserted node is greater than its parent, because we know it
    // can't be equal (it's a freshly inserted node).
    if (parent_block.right_tree_child_ == DataBlockLocation::kInvalid) {
      // The right child is missing, inserting the new node there:
      //
      //   [parent]  |
      //    /    \   |
      // [left]  (*) |
      //          ^ inserting here.
      //
      // If the parent's and grandparent's levels were both 0, this will
      // violate the AA-tree invariant (the level of every right
      // grandchild must be less than that of its grandparent). We don't
      // have to fix it here. If there is a grandparent, and it's
      // located to the left, then they were calling this function
      // recursively from the branch below. from the code located below,
      // and they will attempt to fix the tree there.
      parent_block.right_tree_child_ = new_block_data_location_;
      previous_block_ = &parent_block;
      return;
    }
    Insert(parent_block.right_tree_child_);
    if (!previous_block_)
      previous_block_ = &parent_block;
    // Repairing the invariants of the AA-tree if necessary.
    // Since we updated the right subtree, the only invariant that can
    // be broken here is that the level of every right grandchild must
    // be less than that of its grandparent.
    DataBlockLocation right_location = parent_block.right_tree_child_;
    assert(right_location != DataBlockLocation::kInvalid);
    auto& right_block = GetAt(right_location);
    DataBlockLocation grandchild_r = right_block.right_tree_child_;
    if (grandchild_r != DataBlockLocation::kInvalid &&
        GetAt(grandchild_r).tree_level() == parent_block.tree_level()) {
      assert(right_block.tree_level() == parent_block.tree_level());
      // Repairing it with a "split" operation:
      //
      //   [parent]          [right]   |
      //    /  \             /     \   |
      //   ? [right]    => [parent][r] |
      //      /  \         /     \     |
      //    [l]   [r]     ?      [l]   |

      parent_block.right_tree_child_ = right_block.left_tree_child_;
      right_block.left_tree_child_ = parent_location;
      parent_location = right_location;
      right_block.IncrementTreeLevel();
    }
  }

  Accessor& accessor_;
  const DataBlockLocation new_block_data_location_;
  StateBlockBase& new_block_;
  uint64_t counts_and_hashes_;
  uint32_t index_block_id_;
  IndexSlotLocation index_slot_location_{IndexSlotLocation::kInvalid};
  StateBlockBase* previous_block_{nullptr};
};

class HeaderBlock::KeyBlockInserter : public HeaderBlock::BlockInserter {
 public:
  KeyBlockInserter(Accessor& accessor,
                   AbstractKey& abstract_key,
                   uint64_t key_hash)
      : HeaderBlock::BlockInserter{accessor, key_hash},
        abstract_key_{abstract_key} {
    const auto keys_count_in_slot =
        IndexBlock::GetKeysCount(counts_and_hashes_);
    index_slot_location_ = IndexBlock::MakeIndexSlotLocation(
        index_block_id_, keys_count_in_slot + 1);
    IndexBlock& index = accessor_.GetIndexBlock(index_block_id_);
    index.InitSlot(keys_count_in_slot, new_block_data_location_);

    new (&new_block_) KeyStateBlock{abstract_key.MakeHandle(),
                                    KeySubscriptionHandle::kInvalid};
    PublishToSortedList(accessor.header_block().keys_tree_root_,
                        accessor.header_block().keys_list_head_);
    // Incrementing the keys count and writing the hash byte
    const uint64_t new_counts_and_hashes =
        counts_and_hashes_ +
        ((key_hash & 0xffull) << ((keys_count_in_slot + 1) * 8)) + 1;
    index.counts_and_hashes_.store(new_counts_and_hashes,
                                   std::memory_order_release);
  }

  KeyStateBlock& new_block() noexcept {
    return static_cast<KeyStateBlock&>(new_block_);
  }

 protected:
  bool IsNewBlockLessThan(const StateBlockBase& other) const noexcept override {
    return abstract_key_.IsLessThan(other.key_);
  }

 private:
  const AbstractKey& abstract_key_;
};

class HeaderBlock::SubkeyBlockInserter : public HeaderBlock::BlockInserter {
 public:
  SubkeyBlockInserter(Accessor& accessor,
                      KeyStateBlock& key_block,
                      uint64_t subkey,
                      uint64_t combined_hash)
      : HeaderBlock::BlockInserter{accessor, combined_hash}, subkey_{subkey} {
    const auto subkeys_count_in_slot =
        IndexBlock::GetSubkeysCount(counts_and_hashes_);
    index_slot_location_ = IndexBlock::MakeIndexSlotLocation(
        index_block_id_, 7 - subkeys_count_in_slot);
    IndexBlock& index = accessor.GetIndexBlock(index_block_id_);
    index.InitSlot(6 - subkeys_count_in_slot, new_block_data_location_);

    new (&new_block_) SubkeyStateBlock{
        key_block.key_, SubkeySubscriptionHandle::kInvalid, subkey};
    PublishToSortedList(key_block.subkeys_tree_root_,
                        key_block.subkeys_list_head_);
    // Incrementing the subkeys count and writing the hash byte
    const uint64_t new_counts_and_hashes =
        counts_and_hashes_ +
        ((combined_hash & 0xffull) << ((7 - subkeys_count_in_slot) * 8)) + 8;

    assert(IndexBlock::GetSubkeysCount(new_counts_and_hashes) ==
           IndexBlock::GetSubkeysCount(counts_and_hashes_) + 1);
    index.counts_and_hashes_.store(new_counts_and_hashes,
                                   std::memory_order_release);
  }

  SubkeyStateBlock& new_block() noexcept {
    return static_cast<SubkeyStateBlock&>(new_block_);
  }

 protected:
  bool IsNewBlockLessThan(const StateBlockBase& other) const noexcept override {
    return subkey_ < static_cast<const SubkeyStateBlock&>(other).subkey_;
  }

 private:
  const uint64_t subkey_;
};

#ifdef MS_MR_SHARING_PLATFORM_AMD64
template <IndexLevel kLevel, typename TSearchResult, typename TEqualPredicate>
__forceinline TSearchResult HeaderBlock::Accessor::FindState(
    uint64_t hash,
    TEqualPredicate&& predicate) noexcept {
  const auto hash_8x8 = _mm_set1_epi8(GetSlotHash8FromHash(hash));
  // If we won't find the result in the first block, we'll check this bit to
  // see if we should keep searching.
  uint8_t overflow_mask = IndexBlock::kThisBlockOverflowMask;

  auto& masks = kHashMasks.masks[static_cast<size_t>(kLevel)];

  const uint32_t index_blocks_mask = header_block_.index_blocks_mask_;
  for (uint32_t index_offset = GetIndexOffsetFromHash(hash);; ++index_offset) {
    const uint32_t index_block_id = index_offset & index_blocks_mask;
    const IndexBlock& index_block = index_begin_[index_block_id];
    const auto counts_and_hashes = index_block.counts_and_hashes();
    const auto counts_byte = static_cast<uint8_t>(counts_and_hashes);
    // Bits [1..7] are set for slots [0..6] where the hash matches the 8-bit
    // prefix, and the slot contains a SubkeyState.
    uint32_t mask = masks[counts_byte] &
                    _mm_movemask_epi8(_mm_cmpeq_epi8(
                        hash_8x8, _mm_cvtsi64_si128(counts_and_hashes)));
    unsigned long first_bit_id;
    while (_BitScanForward(&first_bit_id, mask)) {
      // Here we have a mask that indicates which hash bytes match the hash we
      // were looking for. However, bit 0 is always 0 since it's not
      // associated with any hash and is always masked out (it's counts_byte,
      // see above). Therefore, to get the slot id we should subtract 1 from
      // first_bit_id.
      const IndexBlock::Slot& slot =
          index_block.GetSlot(static_cast<size_t>(first_bit_id) - 1);
      const DataBlockLocation version_block_location =
          slot.version_block_location_.load(std::memory_order_acquire);
      auto& state_block =
          GetBlockAt<StateBlock<kLevel>>(slot.state_block_location_);
      VersionBlock<kLevel>* version_block = nullptr;

      if (version_block_location != DataBlockLocation::kInvalid) {
        version_block =
            &GetBlockAt<VersionBlock<kLevel>>(version_block_location);
        Platform::Prefetch(&version_block);
      }
      if (predicate(state_block)) {
        return {IndexBlock::MakeIndexSlotLocation(index_block_id, first_bit_id),
                &state_block, version_block};
      }
      // Clearing the bit that we just checked.
      mask ^= 1u << first_bit_id;
    }
    if ((overflow_mask & counts_byte) == 0) {
      // This block didn't satisfy the search request, and the search hint
      // tells that it's pointless to keep searching.
      return {};
    }
    // From the next iteration we'll be checking a different bit for the index
    // overflow.
    overflow_mask = IndexBlock::kPrecedingBlocksOverflowMask;
  }
  // This method is executed on either partially populated blobs or blobs with a
  // single index block, therefore the loop above should stop eventually, either
  // because the slot has been found, or because the overflow bit is missing.
}
#endif  // #ifdef MS_MR_STATESYNC_PLATFORM_AMD64

KeyBlockStateSearchResult HeaderBlock::Accessor::FindKey(
    const AbstractKey& key) noexcept {
  return FindState<IndexLevel::Key, KeyBlockStateSearchResult>(
      key.hash(), [&key](const KeyStateBlock& candidate) -> bool {
        return key.IsEqualTo(candidate.key_);
      });
}

SubkeyBlockStateSearchResult HeaderBlock::Accessor::FindSubkey(
    const AbstractKey& key,
    uint64_t subkey) noexcept {
  return FindState<IndexLevel::Subkey, SubkeyBlockStateSearchResult>(
      CalculateHash64(key.hash(), subkey),
      [&key, subkey](const SubkeyStateBlock& candidate) -> bool {
        return subkey == candidate.subkey_ && key.IsEqualTo(candidate.key_);
      });
}

KeySearchResult HeaderBlock::Accessor::FindKey(
    uint64_t version,
    const AbstractKey& key) noexcept {
  if (version >= header_block_.base_version_) {
    KeyBlockStateSearchResult search_result = FindKey(key);
    if (search_result.version_block_) {
      return {search_result.index_slot_location_, search_result.state_block_,
              search_result.version_block_->GetSubkeysCount(
                  MakeVersionOffset(version, header_block_.base_version_))};
    } else if (search_result.state_block_) {
      return {search_result.index_slot_location_, search_result.state_block_,
              search_result.state_block_->GetSubkeysCount(
                  MakeVersionOffset(version, header_block_.base_version_))};
    }
  }
  return {};
}

SubkeySearchResult HeaderBlock::Accessor::FindSubkey(uint64_t version,
                                                     const AbstractKey& key,
                                                     uint64_t subkey) noexcept {
  if (version >= header_block_.base_version_) {
    SubkeyBlockStateSearchResult search_result = FindSubkey(key, subkey);
    return {search_result.index_slot_location_, search_result.state_block_,
            search_result.GetVersionedPayload(version)};
  }
  return {};
}

KeyBlockStateSearchResult HeaderBlock::Accessor::InsertKeyBlock(
    AbstractKey& key) noexcept {
  assert(header_block_.CanInsertStateBlocks(1));
  assert(FindKey(key).index_slot_location_ == IndexSlotLocation::kInvalid);

  KeyBlockInserter inserter{*this, key, key.hash()};
  assert(inserter.index_slot_location() != IndexSlotLocation::kInvalid);
  return {inserter.index_slot_location(), &inserter.new_block(), nullptr};
}

SubkeyBlockStateSearchResult HeaderBlock::Accessor::InsertSubkeyBlock(
    Behavior& behavior,
    KeyStateBlock& key_block,
    uint64_t subkey) noexcept {
  assert(header_block_.CanInsertStateBlocks(1));
  assert(
      FindSubkey(AbstractKeyWithHandle{behavior, key_block.key_, false}, subkey)
          .index_slot_location_ == IndexSlotLocation::kInvalid);

  SubkeyBlockInserter inserter(
      *this, key_block, subkey,
      CalculateHash64(behavior.GetHash(key_block.key_), subkey));
  assert(inserter.index_slot_location() != IndexSlotLocation::kInvalid);
  return {inserter.index_slot_location(), &inserter.new_block(), nullptr};
}

bool HeaderBlock::Accessor::ReserveSpaceForTransaction(
    KeyBlockStateSearchResult& search_result) noexcept {
  KeyStateBlock& state_block = *search_result.state_block_;
  KeyVersionBlock* const version_block = search_result.version_block_;
  if (version_block) {
    if (version_block->HasEmptySlots()) {
      return true;
    }
  } else if (state_block.HasFreeInPlaceSlots()) {
    return true;
  }
  uint32_t available_data_blocks_count =
      header_block_.available_data_blocks_count();
  if (available_data_blocks_count == 0)
    return false;

  const DataBlockLocation new_version_block_location{
      header_block_.stored_data_blocks_count_};

  IndexBlock::Slot& index_block_slot =
      IndexBlock::GetSlot(index_begin_, search_result.index_slot_location_);

  DataBlockLocation previous_version_block_location =
      index_block_slot.version_block_location_.load(std::memory_order_relaxed);

  KeyVersionBlock& new_version_block =
      GetBlockAt<KeyVersionBlock>(new_version_block_location);

  KeyVersionBlock::Builder builder{new_version_block,
                                   available_data_blocks_count,
                                   header_block_.stored_data_blocks_count_};

  VersionRefCount::Accessor version_ref_count_accessor =
      header_block_.version_ref_count_accessor();

  const auto stored_versions_count = header_block_.stored_versions_count();

  // Copying all alive versions to the new block.
  if (version_block) {
    if (version_ref_count_accessor.ForEachAliveVersion(
            stored_versions_count - 1, [&](VersionOffset offset) {
              return !builder.Push(offset,
                                   version_block->GetSubkeysCount(offset));
            })) {
      return false;
    }
  } else if (version_ref_count_accessor.ForEachAliveVersion(
                 stored_versions_count - 1, [&](VersionOffset offset) {
                   return !builder.Push(offset,
                                        state_block.GetSubkeysCount(offset));
                 })) {
    return false;
  }
  if (!builder.FinalizeAndReserveOne())
    return false;

  // Publishing the new version block that now contains all alive version and an
  // extra slot for the new version that will be inserted.
  index_block_slot.version_block_location_.store(new_version_block_location,
                                                 std::memory_order_release);
  search_result.version_block_ = &new_version_block;
  return true;
}

bool HeaderBlock::Accessor::ReserveSpaceForTransaction(
    SubkeyBlockStateSearchResult& search_result,
    uint64_t new_version,
    bool has_value) noexcept {
  VersionedPayloadHandle current_payload =
      search_result.version_block_
          ? search_result.version_block_->GetLatestVersionedPayload()
          : search_result.state_block_->GetLatestVersionedPayload();
  SubkeyStateBlock& state_block = *search_result.state_block_;
  SubkeyVersionBlock* const version_block = search_result.version_block_;

  if (version_block) {
    if (version_block->CanPush(new_version, has_value)) {
      return true;
    }
  } else if (state_block.CanPush(new_version, has_value)) {
    return true;
  }
  // Allocating new data blocks from the free data blocks in this blob.
  // (starting with one block, and adding additional ones if necessary).
  uint32_t available_data_blocks_count =
      header_block_.available_data_blocks_count();
  if (available_data_blocks_count == 0)
    return false;

  const DataBlockLocation new_version_block_location{
      header_block_.stored_data_blocks_count_};

  IndexBlock::Slot& index_block_slot =
      IndexBlock::GetSlot(index_begin_, search_result.index_slot_location_);

  DataBlockLocation previous_version_block_location =
      index_block_slot.version_block_location_.load(std::memory_order_relaxed);

  SubkeyVersionBlock& new_version_block =
      GetBlockAt<SubkeyVersionBlock>(new_version_block_location);

  SubkeyVersionBlock::Builder builder{
      previous_version_block_location, new_version_block,
      available_data_blocks_count, header_block_.stored_data_blocks_count_};

  VersionRefCount::Accessor version_ref_count_accessor =
      header_block_.version_ref_count_accessor();

  const auto stored_versions_count = header_block_.stored_versions_count();

  // Copying all alive versions to the new block.
  if (version_block) {
    if (version_ref_count_accessor.ForEachAliveVersion(
            stored_versions_count - 1, [&](VersionOffset offset) {
              const uint64_t version =
                  header_block_.base_version_ + static_cast<uint32_t>(offset);
              return !builder.Push(version,
                                   version_block->GetVersionedPayload(version));
            })) {
      return false;
    }
  } else if (version_ref_count_accessor.ForEachAliveVersion(
                 stored_versions_count - 1, [&](VersionOffset offset) {
                   const uint64_t version = header_block_.base_version_ +
                                            static_cast<uint32_t>(offset);
                   return !builder.Push(
                       version, state_block.GetVersionedPayload(version));
                 })) {
    return false;
  }
  if (!builder.FinalizeAndReserveOne(new_version, has_value))
    return false;

  // Publishing the new version block that now contains all alive version and an
  // extra slot for the new version that will be inserted.
  index_block_slot.version_block_location_.store(new_version_block_location,
                                                 std::memory_order_release);
  search_result.version_block_ = &new_version_block;
  return true;
}

KeyStateBlockEnumerator
HeaderBlock::Accessor::CreateKeyStateBlockEnumerator() noexcept {
  return {header_block_.keys_list_head_.load(std::memory_order_acquire),
          index_begin_, data_begin_, header_block_.base_version_};
}

SubkeyStateBlockEnumerator
HeaderBlock::Accessor::CreateSubkeyStateBlockEnumerator(
    const KeyBlockStateSearchResult& search_result) noexcept {
  IndexSlotLocation location =
      search_result.state_block_
          ? search_result.state_block_->subkeys_list_head_.load(
                std::memory_order_acquire)
          : IndexSlotLocation::kInvalid;
  return {location, index_begin_, data_begin_};
}

HeaderBlock::HeaderBlock(uint64_t base_version,
                         uint32_t index_blocks_mask,
                         uint32_t index_slots_capacity,
                         uint32_t data_blocks_capacity)
    : base_version_{base_version},
      index_blocks_mask_{index_blocks_mask},
      remaining_index_slots_capacity_{index_slots_capacity},
      data_blocks_capacity_{data_blocks_capacity} {
  version_ref_count_accessor().InitVersion(VersionOffset{0});
}

bool HeaderBlock::AddVersion() noexcept {
  uint32_t new_version_id = stored_versions_count();
  if (new_version_id == ~0ull) {
    // Even if there are empty data blocks, the new version won't be
    // compressible as an offset from the base version.
    return false;
  }

  assert(data_blocks_capacity_ > stored_data_blocks_count_);
  const uint32_t blocks_available_for_versions =
      data_blocks_capacity_ - stored_data_blocks_count_;
  if (blocks_available_for_versions < 0x1000'0000 &&
      new_version_id ==
          blocks_available_for_versions * VersionRefCount::kCountsPerBlock) {
    return false;
  }
  alive_snapshots_count_.fetch_add(1, std::memory_order_relaxed);
  version_ref_count_accessor().InitVersion(VersionOffset{new_version_id});
  stored_versions_count_.store(new_version_id + 1, std::memory_order_release);
  return true;
}

void HeaderBlock::RemoveSnapshotReference(uint64_t version,
                                          Behavior& behavior) noexcept {
  assert(IsVersionFromThisBlock(version));

  VersionOffset version_offset{static_cast<uint32_t>(version - base_version_)};
  if (version_ref_count_accessor().RemoveReference(version_offset)) {
    if (1 == alive_snapshots_count_.fetch_sub(1, std::memory_order_acq_rel)) {
      // This was the last reference, the whole blob can be safely destroyed.

      Accessor accessor(*this);
      // We are doing two passes. First pass cleans up all subkeys, second
      // pass cleans up all keys. This is done in case subkeys' payloads
      // contain non-owning pointers to keys that are referenced from the
      // destructor.
      for (uint32_t index_block_id = 0; index_block_id <= index_blocks_mask_;
           ++index_block_id) {
        const IndexBlock& index = accessor.GetIndexBlock(index_block_id);
        const auto counts_and_hashes = index.counts_and_hashes();
        const size_t subkeys_count =
            IndexBlock::GetSubkeysCount(counts_and_hashes);
        for (size_t i = 0; i < subkeys_count; ++i) {
          auto& slot = index.GetSlot(6 - i);
          // Note that we don't have to release keys since they are not owned
          // by subkey blocks. Corresponding key blocks own them instead (see
          // below).
          const auto& state_block =
              accessor.GetBlockAt<SubkeyStateBlock>(slot.state_block_location_);
          Platform::Prefetch(&state_block);
          DataBlockLocation version_block_location =
              slot.version_block_location_.load(std::memory_order_relaxed);

          // FIXME: should reuse the same vector for all blocks.
          auto payloads = state_block.GetAllPayloads();
          if (state_block.has_subscription()) {
            behavior.Release(state_block.subscription());
          }
          if (version_block_location != DataBlockLocation::kInvalid) {
            do {
              auto& version_block = accessor.GetBlockAt<SubkeyVersionBlock>(
                  version_block_location);
              version_block_location = version_block.AppendPayloads(payloads);
            } while (version_block_location != DataBlockLocation::kInvalid);

            // Versions that survived several reallocations will occur several
            // times. Since we don't increment the refcount of payloads on
            // reallocations, we should remove the duplicates here.
            std::sort(begin(payloads), end(payloads),
                      [](const auto& a, const auto& b) {
                        return a.version() < b.version();
                      });
            const auto it_end = std::unique(begin(payloads), end(payloads),
                                            [](const auto& a, const auto& b) {
                                              return a.version() == b.version();
                                            });
            for (auto it = begin(payloads); it != it_end; ++it) {
              behavior.Release(it->payload());
            }
          } else {
            for (auto& payload : payloads) {
              behavior.Release(payload.payload());
            }
          }
        }
      }
      // Second pass releases all keys owned by key blocks.
      for (uint32_t index_block_id = 0; index_block_id <= index_blocks_mask_;
           ++index_block_id) {
        const IndexBlock& index = accessor.GetIndexBlock(index_block_id);
        const auto counts_and_hashes = index.counts_and_hashes();
        const size_t keys_count = IndexBlock::GetKeysCount(counts_and_hashes);
        for (size_t i = 0; i < keys_count; ++i) {
          auto& slot = index.GetSlot(i);
          const auto& state_block =
              accessor.GetBlockAt<KeyStateBlock>(slot.state_block_location_);
          if (state_block.has_subscription()) {
            behavior.Release(state_block.subscription());
          }
          behavior.Release(state_block.key_);
        }
      }

      this->~HeaderBlock();
      behavior.FreePages(this);
    }
  }
}

bool HeaderBlock::CanInsertStateBlocks(size_t extra_state_blocks_count) const
    noexcept {
  // Each state block will also require one index slot
  if (extra_state_blocks_count > remaining_index_slots_capacity_)
    return false;

  return extra_state_blocks_count <=
         data_blocks_capacity_ - stored_data_blocks_count_ -
             (stored_versions_count() / VersionRefCount::kCountsPerBlock);
}

HeaderBlock* HeaderBlock::CreateBlob(Behavior& behavior,
                                     uint64_t base_version,
                                     size_t min_index_capacity) noexcept {
  static constexpr size_t kMaxBlobCapacity = 0x8000'0000;
  if (min_index_capacity > kMaxBlobCapacity)
    return nullptr;
  uint32_t index_capacity = 7;
  uint32_t index_blocks_count_log2 = 0;
  if (min_index_capacity > IndexBlock::kSlotsPerBlock) {
    // It's ok to have a 100% load factor if all elements fit into one index
    // block.
    // Otherwise we'll maintain ~57% load factor for the index (4 out of 7
    // slots are occupied on average when the index is full).

    index_capacity = 8;
    index_blocks_count_log2 = 1;
    // Inefficient way that should be good enough.
    while (index_capacity < min_index_capacity) {
      index_capacity *= 2;
      ++index_blocks_count_log2;
    }
  }
  const uint32_t index_blocks_count = 1u << index_blocks_count_log2;
  constexpr uint32_t kBlocksPerPage = Platform::kPageSize / kBlockSize;
  // Each index entry should have a data block.
  // On top of that we want about the same amount of space for the version
  // blocks, and an extra block for the header.
  const uint32_t pages_count =
      (index_capacity * 2 + index_blocks_count) / kBlocksPerPage + 1;
  auto header =
      static_cast<HeaderBlock*>(behavior.AllocateZeroedPages(pages_count));
  if (header) {
    const uint32_t data_blocks_capacity =
        pages_count * kBlocksPerPage - index_blocks_count - 1;
    new (header) HeaderBlock{base_version, index_blocks_count - 1,
                             index_capacity, data_blocks_capacity};
    // The empty state of HashBlock requires the memory to be zeroed, and we
    // won't be accessing any other blocks before constructing them.
    // It's safe to skip the construction of all the other blocks.
  }
  return header;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage