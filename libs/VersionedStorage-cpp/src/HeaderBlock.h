// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/AbstractKey.h>

#include "src/IndexBlock.h"
#include "src/SearchResult.h"
#include "src/StateBlockEnumerator.h"
#include "src/VersionRefCount.h"

#include <cstddef>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Behavior;
class IndexBlock;

// The first block of any storage blob, contains blob-wide layout
// characteristics.
class alignas(kBlockSize) HeaderBlock {
 public:
  HeaderBlock(uint64_t base_version,
              uint32_t index_blocks_mask,
              uint32_t index_slots_capacity,
              uint32_t data_blocks_capacity);

  class Accessor;

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

  // Should only be called by the writer thread
  auto& keys_count() noexcept { return keys_count_; }
  auto keys_count() const noexcept { return keys_count_; }

  // Should only be called by the writer thread
  auto& subkeys_count() noexcept { return subkeys_count_; }
  auto subkeys_count() const noexcept { return subkeys_count_; }

  // Should only be called by the writer thread
  auto remaining_index_slots_capacity() const noexcept {
    return remaining_index_slots_capacity_;
  }

  auto index_blocks_mask() const noexcept { return index_blocks_mask_; }

  auto data_blocks_capacity() const noexcept { return data_blocks_capacity_; }

  // Returns the number of data blocks available for allocation.
  // Should only be called by the writer thread.
  uint32_t available_data_blocks_count() const noexcept;

  // Can report a false positive if called before the version blocks for
  // existing states are allocated, so it generally should be called twice:
  // before reserving and after reserving.
  // Should only be called by the writer thread.
  [[nodiscard]] bool CanInsertStateBlocks(size_t extra_state_blocks_count) const
      noexcept;

  // Attempts to add a version.
  // On success, the the new version's reference count will be 1.
  // On failure (which can happen if there is not enough space to store the new
  // version), no reference counts will be changed.
  // Should only be called by the writer thread.
  [[nodiscard]] bool AddVersion() noexcept;

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
  bool is_mutable_mode() const noexcept { return is_mutable_mode_; }

  void SetImmutableMode() noexcept {
    assert(is_mutable_mode_);
    is_mutable_mode_ = false;
  }

 private:
  DataBlockLocation AllocateDataBlock() noexcept;

  VersionRefCount::Accessor version_ref_count_accessor() noexcept {
    return {15 + reinterpret_cast<VersionRefCount*>(this + index_blocks_mask_ +
                                                    data_blocks_capacity_ + 1)};
  }

  bool IsVersionFromThisBlock(uint64_t version) const noexcept;

  class BlockInserter;
  class KeyBlockInserter;
  class SubkeyBlockInserter;

  const uint64_t base_version_;
  mutable std::atomic_uint32_t alive_snapshots_count_{1};
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
};

static_assert(sizeof(HeaderBlock) == kBlockSize);

class HeaderBlock::Accessor {
 public:
  Accessor(HeaderBlock& header_block)
      : header_block_{header_block},
        index_begin_{reinterpret_cast<IndexBlock*>(&header_block + 1)},
        data_begin_{reinterpret_cast<std::byte*>(
            &header_block +
            static_cast<size_t>(header_block_.index_blocks_mask_) + 2)} {}

  HeaderBlock& header_block() noexcept { return header_block_; }

  template <IndexLevel kLevel, typename TSearchResult, typename TEqualPredicate>
  TSearchResult FindState(uint64_t hash, TEqualPredicate&& predicate) noexcept;

  template <typename TBlock>
  TBlock& GetBlockAt(DataBlockLocation location) noexcept {
    return VersionedStorage::GetBlockAt<TBlock>(data_begin_, location);
  }

  IndexBlock& GetIndexBlock(uint32_t index_block_id) {
    return index_begin_[index_block_id];
  }

  KeyBlockStateSearchResult FindKey(const AbstractKey& key) noexcept;

  SubkeyBlockStateSearchResult FindSubkey(const AbstractKey& key,
                                          uint64_t subkey) noexcept;

  KeySearchResult FindKey(uint64_t version, const AbstractKey& key) noexcept;

  SubkeySearchResult FindSubkey(uint64_t version,
                                const AbstractKey& key,
                                uint64_t subkey) noexcept;

  // The key must be missing and there must be enough capacity.
  KeyBlockStateSearchResult InsertKeyBlock(AbstractKey& key) noexcept;

  // The subkey must be missing and there must be enough capacity.
  SubkeyBlockStateSearchResult InsertSubkeyBlock(Behavior& behavior,
                                                 KeyStateBlock& key_block,
                                                 uint64_t subkey) noexcept;

  [[nodiscard]] bool ReserveSpaceForTransaction(
      KeyBlockStateSearchResult& search_result) noexcept;

  // Checks the preconditions and attempts to reserve the space necessary to
  // complete the transaction. See PrepareTransactionResult for all possible
  // outcomes. Note that if ReadyToStartTransaction is returned, pushing a new
  // version for the subkey later can't fail. However, we can't do it here
  // immediately, since other keys and subkeys may have to be checked.
  //
  // The provided search_result will be updated if the operation had to
  // reallocate the version block. If search_result has no version block after
  // the operation, that means that the new version can be stored in the state
  // block.
  [[nodiscard]] bool ReserveSpaceForTransaction(
      SubkeyBlockStateSearchResult& search_result,
      uint64_t new_version,
      bool has_value) noexcept;

  [[nodiscard]] KeyStateBlockEnumerator
  CreateKeyStateBlockEnumerator() noexcept;

  [[nodiscard]] SubkeyStateBlockEnumerator CreateSubkeyStateBlockEnumerator(
      const KeyBlockStateSearchResult& search_result) noexcept;

  [[nodiscard]] SubkeyStateBlockEnumerator CreateSubkeyStateBlockEnumerator(
      const AbstractKey& key) noexcept {
    return CreateSubkeyStateBlockEnumerator(FindKey(key));
  }

 private:
  HeaderBlock& header_block_;
  IndexBlock* const index_begin_;
  std::byte* const data_begin_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
