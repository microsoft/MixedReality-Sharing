// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptor.h>

#include "src/IndexBlock.h"
#include "src/SearchResult.h"
#include "src/StateBlockEnumerator.h"
#include "src/VersionRefCount.h"

#include <cstddef>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Behavior;
class IndexBlock;

class BlobAccessor;
class MutatingBlobAccessor;

// The first block of any storage blob, contains blob-wide layout
// characteristics.
class alignas(kBlockSize) HeaderBlock {
 public:
  HeaderBlock(uint64_t base_version,
              uint32_t index_blocks_mask,
              uint32_t index_slots_capacity,
              uint32_t data_blocks_capacity);

  // Creates a new blob and returns the pointer to its HeaderBlock.
  // Both the block's reference count and the base_version's reference
  // count are 1.
  [[nodiscard]] static HeaderBlock* CreateBlob(
      Behavior& behavior,
      uint64_t base_version,
      size_t min_index_capacity) noexcept;

  uint64_t base_version() const noexcept { return base_version_; }

  // Thread-safe.
  void RemoveSnapshotReference(uint64_t version, Behavior& behavior) noexcept;

  uint32_t stored_versions_count() const noexcept {
    return stored_versions_count_.load(std::memory_order_relaxed);
  }

  auto index_blocks_mask() const noexcept { return index_blocks_mask_; }
  auto data_blocks_capacity() const noexcept { return data_blocks_capacity_; }

 private:
  VersionRefCount::Accessor version_ref_count_accessor() noexcept {
    // Accessor is constructed from the position of the refcount of the base
    // version, which is located at the end of the blob, see VersionRefCount.h
    // for details.
    return {15 + reinterpret_cast<VersionRefCount*>(this + index_blocks_mask_ +
                                                    data_blocks_capacity_ + 1)};
  }

  bool IsVersionFromThisBlob(uint64_t version) const noexcept;

  class BlockInserter;
  class KeyBlockInserter;
  class SubkeyBlockInserter;

  const uint64_t base_version_;
  mutable std::atomic_uint32_t alive_snapshots_count_{1};

  // Always 1 less than the number of index blocks (which is a power of two),
  // mainly used to convert hashes into index block positions.
  const uint32_t index_blocks_mask_;
  uint32_t remaining_index_slots_capacity_;

  // Data blocks are consumed from two sides.
  // From the front, they are used to store reference counts for versions.
  // From the back, they are used to store state and version blocks.
  const uint32_t data_blocks_capacity_;
  uint32_t stored_data_blocks_count_{0};
  std::atomic_uint32_t stored_versions_count_{1};

  uint32_t keys_count_{0};
  uint32_t subkeys_count_{0};

  // The head of the linked list of keys, in iteration order.
  std::atomic<IndexSlotLocation> keys_list_head_{IndexSlotLocation::kInvalid};

  // The root of the AA-tree of keys, which is used for the fast insertion into
  // the list above.
  DataBlockLocation keys_tree_root_{DataBlockLocation::kInvalid};
  bool is_mutable_mode_{true};

  friend class BlobAccessor;
  friend class MutatingBlobAccessor;
};

static_assert(sizeof(HeaderBlock) == kBlockSize);

class BlobAccessor {
 public:
  BlobAccessor(HeaderBlock& header_block)
      : header_block_{header_block},
        index_begin_{reinterpret_cast<IndexBlock*>(&header_block + 1)},
        data_begin_{reinterpret_cast<std::byte*>(
            &header_block +
            static_cast<size_t>(header_block_.index_blocks_mask_) + 2)} {}

  HeaderBlock& header_block() noexcept { return header_block_; }
  auto base_version() const noexcept { return header_block_.base_version(); }

  template <typename TBlock>
  TBlock& GetBlockAt(DataBlockLocation location) noexcept {
    return VersionedStorage::GetBlockAt<TBlock>(data_begin_, location);
  }

  IndexBlock& GetIndexBlock(uint32_t index_block_id) {
    return index_begin_[index_block_id];
  }

  KeyBlockStateSearchResult FindKey(const KeyDescriptor& key) noexcept;

  SubkeyBlockStateSearchResult FindSubkey(const KeyDescriptor& key,
                                          uint64_t subkey) noexcept;

  KeySearchResult FindKey(uint64_t version, const KeyDescriptor& key) noexcept;

  SubkeySearchResult FindSubkey(uint64_t version,
                                const KeyDescriptor& key,
                                uint64_t subkey) noexcept;

  [[nodiscard]] KeyStateBlockEnumerator
  CreateKeyStateBlockEnumerator() noexcept;

  [[nodiscard]] SubkeyStateBlockEnumerator CreateSubkeyStateBlockEnumerator(
      const KeyBlockStateSearchResult& search_result) noexcept;

  [[nodiscard]] SubkeyStateBlockEnumerator CreateSubkeyStateBlockEnumerator(
      const KeyDescriptor& key) noexcept {
    return CreateSubkeyStateBlockEnumerator(FindKey(key));
  }

  struct IndexOffsetAndSlotHashes {
    IndexOffsetAndSlotHashes(uint64_t key_hash);
    IndexOffsetAndSlotHashes(uint64_t key_hash, uint64_t subkey);

    // Offset to the first checked index block (the mask will be applied before
    // the search, so the offset effectively wraps around).
    // In the majority of the cases, only some lower bits will be used
    // (depending on how large the index is).
    uint32_t index_offset_hash;

    // A small hash used to quickly filter out slots that definitely don't
    // match (each slot preserves this small hash on insertion, so it can be
    // compared during the search).
    // The quality requirements are not very high (the worst thing that can
    // happen is that we'll have a lot of false positives which will trigger
    // full key/subkey comparisons), but the search will be faster if the
    // probability of collision here is as close to 1/256 as possible, and there
    // is no strong dependency between slot_hash and actually used bits of
    // index_offset_hash.
    uint8_t slot_hash;
  };

 protected:
  template <IndexLevel kLevel, typename TSearchResult, typename TEqualPredicate>
  TSearchResult FindState(const IndexOffsetAndSlotHashes& hashes,
                          TEqualPredicate&& predicate) noexcept;

  HeaderBlock& header_block_;
  IndexBlock* const index_begin_;
  std::byte* const data_begin_;
};

class MutatingBlobAccessor : public BlobAccessor {
 public:
  using BlobAccessor::BlobAccessor;

  constexpr auto remaining_index_slots_capacity() const noexcept {
    return header_block_.remaining_index_slots_capacity_;
  }

  auto& keys_count() noexcept { return header_block_.keys_count_; }
  auto& subkeys_count() noexcept { return header_block_.subkeys_count_; }

  // Returns the number of data blocks available for allocation.
  uint32_t available_data_blocks_count() const noexcept;

  DataBlockLocation AllocateDataBlock() noexcept;

  // Attempts to add a new version to the blob.
  // On success, the new version's reference count will be 1.
  // On failure (which can happen if there is not enough space to store the new
  // version), no reference counts will be changed.
  [[nodiscard]] bool AddVersion() noexcept;

  [[nodiscard]] bool CanInsertStateBlocks(size_t extra_state_blocks_count) const
      noexcept;

  // The key must be missing and there must be enough capacity.
  KeyBlockStateSearchResult InsertKeyBlock(KeyDescriptor& key) noexcept;

  // The subkey must be missing and there must be enough capacity.
  SubkeyBlockStateSearchResult InsertSubkeyBlock(Behavior& behavior,
                                                 KeyStateBlock& key_block,
                                                 uint64_t subkey) noexcept;

  // The provided search_result will be updated if the operation had to
  // reallocate the version block. If search_result has no version block after
  // the operation, that means that the new version can be stored in the state
  // block.
  [[nodiscard]] bool ReserveSpaceForTransaction(
      KeyBlockStateSearchResult& search_result) noexcept;

  // The provided search_result will be updated if the operation had to
  // reallocate the version block. If search_result has no version block after
  // the operation, that means that the new version can be stored in the state
  // block.
  [[nodiscard]] bool ReserveSpaceForTransaction(
      SubkeyBlockStateSearchResult& search_result,
      uint64_t new_version,
      bool has_value) noexcept;

  // The mode is switched right before the next block is allocated, which
  // happens when the transaction is successfully validated, but fails to
  // allocate enough space in this block.
  // Then following merge of this block and the transaction may switch some of
  // the blocks into the "scratch buffer mode," which makes the internal
  // AA-trees (of key and subkey blocks) unusable. Regardless of the success of
  // the merge, this block can't be used for new versions. Readers can keep
  // reading it, and then the block can be destroyed, but nothing else is
  // allowed.
  // Note that even if the allocation of the new block failed (and the merge
  // wasn't actually performed), this blob stays immutable forever, because no
  // progress can be made for this storage (skipping one transaction due to low
  // memory, and the applying next ones would diverge the state between
  // different machines).
  bool is_mutable_mode() const noexcept {
    return header_block_.is_mutable_mode_;
  }

  void SetImmutableMode() noexcept {
    assert(header_block_.is_mutable_mode_);
    header_block_.is_mutable_mode_ = false;
  }
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
