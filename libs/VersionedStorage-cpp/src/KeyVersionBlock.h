// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include "src/layout.h"

#include <atomic>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

// If the StateBlock for a key runs out of space to store versions,
// one or more consecutive KeyVersionBlocks are allocated.
// Blocks after the first one are treated as a continuation of the array
// that starts in the first block.
// We typically reserve twice more space than we need to ensure that the
// amortized cost of inserting a new version stays constant.
class alignas(kBlockSize) KeyVersionBlock {
 public:
  // Returns the number of subkeys of a key for a given version_offset.
  uint32_t GetSubkeysCount(VersionOffset version_offset) const noexcept;

  uint32_t latest_subkeys_count_thread_unsafe() const noexcept {
    uint32_t size = size_.load(std::memory_order_relaxed);
    return size == 0 ? 0 : versioned_subkey_counts_[size - 1].subkeys_count;
  }

  bool has_empty_slots_thread_unsafe() const noexcept {
    return size_.load(std::memory_order_relaxed) < capacity_;
  }

  // Should only be called if the subkeys count doesn't match the latest subkeys
  // count.
  void PushSubkeysCountFromWriterThread(VersionOffset version_offset,
                                        uint32_t subkeys_count) noexcept;

  class Builder {
   public:
    // Constructs uninitialized_first_block and increments
    // stored_data_blocks_count (it will also be incremented each time a Push
    // operation would require an extra block). available_blocks_count must be
    // at least 1, and the total number of written blocks won't exceed it.
    Builder(KeyVersionBlock& uninitialized_first_block,
            uint32_t available_blocks_count,
            uint32_t& stored_data_blocks_count) noexcept;

    // Attempts to store a version, allocating a new block if necessary.
    // The operation will succeed with no effect if pushed subkeys_count
    // wouldn't change the outcome of the search (so the number of actually
    // stored versions may be less than the number of pushed versions).
    bool Push(VersionOffset version_offset, uint32_t subkeys_count) noexcept;

    // Finalizes the construction by reserving space for at least one extra
    // version and writing the size field with release semantic.
    // Generally tries to reserve enough space to keep the initial load factor
    // around 0.5, but will simply reserve as much as it can if there are not
    // enough free blocks.
    // Returns false if no extra free slots were reserved (blocks already
    // consumed by the builder will be in uspecified state; the caller is
    // expected to abandon them and reallocate the entire storage blob).
    bool FinalizeAndReserveOne() noexcept;

   private:
    KeyVersionBlock& first_block_;
    uint32_t available_blocks_count_;
    uint32_t& stored_data_blocks_count_;
    uint32_t size_ = 0;
    uint32_t capacity_ = 7;
  };

  // For testing.
  uint32_t size_relaxed() const noexcept {
    return size_.load(std::memory_order_relaxed);
  }

  // For testing.
  uint32_t capacity() const noexcept { return capacity_; }

 private:
  KeyVersionBlock() = default;

  // All members are initialized by the builder.
  std::atomic_uint32_t size_;
  uint32_t capacity_;
  // If more than one block is allocated, we assume that this array continues,
  // and each next block stores up to 8 more elements.
  VersionedSubkeysCount versioned_subkey_counts_[7];
};
static_assert(sizeof(KeyVersionBlock) == kBlockSize);

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
