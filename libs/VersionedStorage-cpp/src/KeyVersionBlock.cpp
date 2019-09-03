// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include "src/KeyVersionBlock.h"

#include <algorithm>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

uint32_t KeyVersionBlock::GetSubkeysCount(VersionOffset version_offset) const
    noexcept {
  // memory_order_acquire due to non-atomic reads below.
  uint32_t size = size_.load(std::memory_order_acquire);
  const VersionedSubkeysCount* ptr =
      std::upper_bound(versioned_subkey_counts_,
                       versioned_subkey_counts_ + size, version_offset);
  return ptr == versioned_subkey_counts_ ? 0 : ptr[-1].subkeys_count;
}

void KeyVersionBlock::PushSubkeysCount(VersionOffset version_offset,
                                       uint32_t subkeys_count) noexcept {
  assert(HasEmptySlots() && GetLatestSubkeysCount() != subkeys_count);
  // memory_order_relaxed since this can only be called by the writer thread.
  uint32_t size = size_.load(std::memory_order_relaxed);
  versioned_subkey_counts_[size] = {version_offset, subkeys_count};
  // Publishing the non-atomic writes made above.
  size_.store(size + 1, std::memory_order_release);
}

KeyVersionBlock::Builder::Builder(KeyVersionBlock& uninitialized_first_block,
                                  uint32_t avaliable_blocks_count,
                                  uint32_t& stored_data_blocks_count) noexcept
    : first_block_{uninitialized_first_block},
      avaliable_blocks_count_{avaliable_blocks_count - 1},
      stored_data_blocks_count_{stored_data_blocks_count} {
  assert(avaliable_blocks_count > 0);
  ++stored_data_blocks_count;
  new (&uninitialized_first_block) KeyVersionBlock;
}

bool KeyVersionBlock::Builder::Push(VersionOffset version_offset,
                                    uint32_t subkeys_count) noexcept {
  if (size_) {
    assert(first_block_.versioned_subkey_counts_[size_ - 1].version_offset <
           version_offset);
    if (subkeys_count ==
        first_block_.versioned_subkey_counts_[size_ - 1].subkeys_count) {
      return true;
    }
  } else if (subkeys_count == 0) {
    return true;
  }
  if (size_ == capacity_) {
    if (avaliable_blocks_count_ == 0) {
      return false;
    }
    --avaliable_blocks_count_;
    ++stored_data_blocks_count_;
    capacity_ += 8;
  }
  first_block_.versioned_subkey_counts_[size_++] = {version_offset,
                                                    subkeys_count};
  return true;
}

bool KeyVersionBlock::Builder::FinalizeAndReserveOne() noexcept {
  const uint32_t optimal_blocks_count = 1 + size_ / 4;
  const uint32_t current_blocks_count = (capacity_ + 1) / 8;
  assert(current_blocks_count <= optimal_blocks_count);
  uint32_t extra_blocks_count = optimal_blocks_count - current_blocks_count;
  if (extra_blocks_count > avaliable_blocks_count_) {
    if (avaliable_blocks_count_ == 0 && size_ == capacity_) {
      // Can't reserve an extra free slot.
      return false;
    }
    // Reserving as much as we can.
    extra_blocks_count = avaliable_blocks_count_;
  }
  capacity_ += extra_blocks_count * 8;
  stored_data_blocks_count_ += extra_blocks_count;
  assert(size_ < capacity_);  // There is space for at least one extra version.
  first_block_.capacity_ = capacity_;
  // memory_order_release to publish all non-atomic writes made by the builder.
  // (reader threads will load this with memory_order_acquire first before
  // accessing anything else).
  first_block_.size_.store(size_, std::memory_order_release);
  return true;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
