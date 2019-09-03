// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include "src/layout.h"

#include <atomic>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// Indexes up to 7 keys/subkeys.
//
// Each slot can be occupied by a pair of state block and version block
// locations. For each slot the index also stores its type (implicitly, encoded
// as the number of used key and subkey slots) and a small 8-bit hash associated
// with it. Using these hashes, the readers can quickly check which slots are
// likely to point to the state block they are looking for, and only then go to
// the block and verify that the key (and possibly the subkey) match.
//
// Since the location of the version block is stored in the index (and not, for
// example, in the state block), the reader can start prefetching the version
// block before the state block is obtained and checked. This reduces the
// overall time spent on waiting for the memory when performing a random search.
//
// Since there could be multiple observers of various versions of the state (and
// some of the observers can work with elements deleted in new versions), the
// index doesn't support deletion. New elements are inserted into index until it
// runs out of capacity (or the blob runs out of capacity for data blocks). Then
// the new blob is allocated, and keys and subkeys that are still alive are
// copied there. Then the old blob gets deallocated when none of its versions
// are referenced. This strategy ensures that old keys and subkeys are
// eventually getting deleted, and the index, on average, doesn't contain too
// many irrelevant entries.

class alignas(kBlockSize) IndexBlock {
 public:
  struct Slot {
    // Location of either KeyStateBlock or SubkeyStateBlock.
    DataBlockLocation state_block_location_;

    // Location of the first block of the sequence of VersionInfo blocks
    // associated with the slot.
    // If this location is invalid
    // Up to two versions are stored in the state
    // block, so this can be Invalid if no extra blocks are required.
    std::atomic<DataBlockLocation> version_block_location_;
  };

  uint64_t counts_and_hashes_relaxed() const noexcept {
    return counts_and_hashes_.load(std::memory_order_relaxed);
  }

  const Slot& GetSlot(size_t id) const noexcept { return slots_[id]; }

  void InitSlot(size_t id, DataBlockLocation state_block_location) noexcept {
    auto& slot = slots_[id];
    slot.state_block_location_ = state_block_location;
    slot.version_block_location_.store(DataBlockLocation::kInvalid,
                                       std::memory_order_release);
  }

  template <typename T>
  static constexpr uint32_t GetKeysCount(
      T counts_and_hashes_snapshot) noexcept {
    // Bits [0..2]
    return static_cast<uint32_t>(counts_and_hashes_snapshot) & 7;
  }

  template <typename T>
  static constexpr uint32_t GetSubkeysCount(
      T counts_and_hashes_snapshot) noexcept {
    // Bits [3..5]
    return (static_cast<uint32_t>(counts_and_hashes_snapshot) & 0x38u) >> 3;
  }

  template <typename T>
  static constexpr bool HasFreeSlots(T counts_and_hashes_snapshot) noexcept {
    const auto snapshot32 = static_cast<uint32_t>(counts_and_hashes_snapshot);
    return ((~(snapshot32 + (snapshot32 >> 3))) & 7) != 0;
  }

  static constexpr Slot& GetSlot(IndexBlock* blocks,
                                 IndexSlotLocation location) noexcept {
    return blocks[static_cast<uint32_t>(location) >> 3]
        .slots_[(static_cast<uint32_t>(location) & 7) - 1];
  }

  static constexpr IndexSlotLocation MakeIndexSlotLocation(
      uint32_t index_block_id,
      uint32_t bit_id) {
    return IndexSlotLocation{(index_block_id << 3) | bit_id};
  }

  static constexpr uint8_t kSlotsPerBlock = 7;

  // See below
  static constexpr uint8_t kThisBlockOverflowMask = 0x40;
  static constexpr uint8_t kPrecedingBlocksOverflowMask = 0x80;

  // Bits 0..2: number of keys in the block.
  //   Keys will be occupying slots in ascending order.
  // Bits 3..5: number of subkeys in the block.
  //   Subkeys will be occupying slots in descending order.
  // Bit 6: indicates that there were slots that didn't fit into this index
  //        block and were inserted in one of the next blocks.
  // Bit 7: indicates that some of the previous blocks was not able to insert
  //        its elements into itself or this block, so the search should
  //        continue. Note that it's orthogonal to Bit 6.
  // Bytes [1..7] 8-bit hashes for 7 slots (see below).
  //
  // This doesn't have to be initialized since the pages are initially zeroed.
  std::atomic_uint64_t counts_and_hashes_;

 private:
  // Whether the slot contains the information about a key or a subkey depends
  // on first two counters. For example, if the number of keys is 2 and the
  // number of subkeys is 4, kinds of the slots will look like:
  // [key][key][-empty-][subkey][subkey][subkey][subkey]
  Slot slots_[7];
};

static_assert(sizeof(IndexBlock) == kBlockSize);

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
