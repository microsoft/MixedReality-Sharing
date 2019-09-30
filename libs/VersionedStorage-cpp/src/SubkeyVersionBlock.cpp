// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include "src/SubkeyVersionBlock.h"

#include <algorithm>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

// Searching for a version will find the correct block first, by checking the
// first version in the block.

static constexpr bool operator<(const SubkeyVersionBlock& block,
                                uint64_t search_token) noexcept {
  return block.first_marked_version_in_block() < search_token;
}

static constexpr bool operator<(uint64_t search_token,
                                const SubkeyVersionBlock& block) noexcept {
  return search_token < block.first_marked_version_in_block();
}

VersionedPayloadHandle SubkeyVersionBlock::GetVersionedPayload(
    uint64_t version) const noexcept {
  assert(version < kInvalidVersion);

  // We want to find the first payload with marked version that is less or equal
  // to the one we construct here. The search token has the last bit set,
  // so that a deletion marker of the same version can be found if it exists.
  const uint64_t search_token = (static_cast<uint64_t>(version) << 1) | 1;

  // memory_order_acquire due to non-atomic reads below.
  // The writers publish the new size only after all required writes are done,
  // so as long as we are not reading beyond the size, we shouldn't care about
  // ongoing writes.
  const uint32_t size = size_.load(std::memory_order_acquire);
  if (size != 0 && first_marked_version_in_block_ <= search_token) {
    const SubkeyVersionBlock* result_block;
    size_t search_distance;
    if (size < 5) {
      // The first block is special since it can hold only up to 4 versions
      // (we store the size and the capacity instead of the 5th version).
      // This is the most common case for infrequently changing subkeys.
      result_block = this;
      search_distance = size;
    } else {
      const SubkeyVersionBlock* const end = this + 1 + (size / 5);
      const SubkeyVersionBlock* const upper_bound_ptr =
          std::upper_bound(this, end, search_token);
      // Checked above with "first_marked_version_in_block_ > search_token"
      assert(this < upper_bound_ptr);
      result_block = upper_bound_ptr - 1;
      // If the result is in the last used block, we shouldn't read past the
      // size, since the state of the slots there is unspecified. Otherwise we
      // are checking all slots of the block (5, or 4 if that's the first one).
      search_distance = upper_bound_ptr == end ? (size % 5) + 1
                                               : this == result_block ? 4 : 5;
    }
    uint64_t marked_version = result_block->first_marked_version_in_block_;
    assert(search_token >= marked_version);
    // Certain searches could benefit from the binary search here, but they
    // should be a lot less common than searching for the last (or
    // one-before-last) element, so simple iteration in reverse order is better
    // here.
    while (--search_distance) {
      const uint32_t* marked_offsets = result_block->marked_offsets_;
      const uint32_t offset = marked_offsets[search_distance - 1];
      if (offset != kInvalidMarkedOffset) {
        const uint64_t candidate = marked_version + offset;
        if (candidate <= search_token) {
          marked_version = candidate;
          break;
        }
      }
    }
    // Checking that it's not a deletion marker.
    if ((marked_version & 1) == 0) {
      const PayloadHandle* payloads = result_block->payloads_;
      return {marked_version >> 1, payloads[search_distance]};
    }
  }
  // Note that we are not returning the exact version for the deletion marker.
  // For readers, there is no practical difference between missing and deleted
  // subkeys; they are simply not in this version.
  return {};
}

VersionedPayloadHandle
SubkeyVersionBlock::latest_versioned_payload_thread_unsafe() const noexcept {
  // memory_order_relaxed since this can only be called by the writer thread.
  const uint32_t size = size_.load(std::memory_order_relaxed);
  if (size) {
    // This looks suspicious, but it's actually correct since the first block
    // has only 4 elements, and all the remaining ones have 5.
    const SubkeyVersionBlock* last_block = this + (size / 5);
    uint32_t payload_slot_id = size < 5 ? size - 1 : size % 5;

    uint64_t marked_version = last_block->first_marked_version_in_block_;
    if (payload_slot_id) {
      const uint32_t* offsets = last_block->marked_offsets_;
      marked_version += offsets[payload_slot_id - 1];
    };
    if ((marked_version & 1) == 0) {
      const PayloadHandle* payloads = last_block->payloads_;
      return {marked_version >> 1, payloads[payload_slot_id]};
    }
  }
  // Note that we are not returning the exact version for the deletion marker.
  // For readers, there is no practical difference between a missing and deleted
  // subkey; it's simply not in this version.
  return {};
}

bool SubkeyVersionBlock::CanPushFromWriterThread(uint64_t version,
                                                 bool has_payload) const
    noexcept {
  assert(version < kInvalidVersion);
  assert(capacity_ >= 4);
  // memory_order_relaxed since this can only be called by the writer thread.
  uint32_t size = size_.load(std::memory_order_relaxed);
  if (size == 0 || size < capacity_ - 4) {
    // There is at least one completely free block, so we can push any valid
    // version.
    return true;
  }
  if (size == capacity_)
    return false;

  // There are no completely free blocks, but the last block still has some
  // slots. The result depends on whether the version can be compressed as an
  // offset to the base version of the partially free block.
  const uint64_t marked_version =
      (version << 1) | static_cast<uint64_t>(!has_payload);

  const SubkeyVersionBlock& block = this[(size + 1) / 5];
  assert((size + 1) % 5 != 0);
  assert((block.first_marked_version_in_block_ >> 1) < version);

  return (marked_version - block.first_marked_version_in_block_) <
         kInvalidMarkedOffset;
}

void SubkeyVersionBlock::PushFromWriterThread(
    uint64_t version,
    std::optional<PayloadHandle> payload) noexcept {
  assert(CanPushFromWriterThread(version, payload.has_value()));
  // memory_order_relaxed since this can only be called by the writer thread.
  uint32_t size = size_.load(std::memory_order_relaxed);
  SubkeyVersionBlock* block = this + ((size + 1) / 5);
  const uint32_t payload_slot_id = size < 4 ? size : (size + 1) % 5;
  const uint64_t marked_version =
      (version << 1) | static_cast<uint64_t>(!payload.has_value());

  if (payload_slot_id != 0) {
    // This is not the first version in the block, so it should be compressed.
    assert(block->first_marked_version_in_block_ < marked_version);
    uint64_t marked_offset =
        marked_version - block->first_marked_version_in_block_;
    if (marked_offset < kInvalidMarkedOffset) {
      if (payload.has_value()) {
        // Note that this will stay uninitialized if there was no valid payload.
        // The readers will never touch this field since they will be checking
        // the deletion marker bit in the version first.
        PayloadHandle* payloads = block->payloads_;
        payloads[payload_slot_id] = *payload;
      }
      uint32_t* marked_offsets = block->marked_offsets_;
      marked_offsets[payload_slot_id - 1] =
          static_cast<uint32_t>(marked_offset);
      // Publishing the non-atomic writes made above.
      size_.store(size + 1, std::memory_order_release);
      return;
    }
    // The marked version can't be compressed into an offset, and has to
    // become the first version of the next block.
    const uint32_t wasted_slots_count =
        5 - payload_slot_id - static_cast<uint32_t>(this == block);
    uint32_t* marked_offsets = block->marked_offsets_;
    for (uint32_t i = 0; i < wasted_slots_count; ++i) {
      // The readers will be checking for kInvalidMarkedOffset while iterating
      // over marked_offsets to skip the wasted slots.
      marked_offsets[payload_slot_id + i - 1] = kInvalidMarkedOffset;
    }
    size += wasted_slots_count;
    assert(size < capacity_);
    ++block;
  }
  block->first_marked_version_in_block_ = marked_version;
  if (payload.has_value()) {
    block->payloads_[0] = *payload;
  }
  // Publishing the non-atomic writes made above.
  size_.store(size + 1, std::memory_order_release);
}

SubkeyVersionBlock::Builder::Builder(
    DataBlockLocation previous,
    SubkeyVersionBlock& uninitialized_first_block,
    uint32_t available_blocks_count,
    uint32_t& stored_data_blocks_count) noexcept
    : first_block_{uninitialized_first_block},
      available_blocks_count_{available_blocks_count - 1},
      stored_data_blocks_count_{stored_data_blocks_count} {
  assert(available_blocks_count > 0);
  ++stored_data_blocks_count;
  new (&uninitialized_first_block) SubkeyVersionBlock{previous};
}

bool SubkeyVersionBlock::Builder::Push(
    uint64_t version,
    VersionedPayloadHandle observed_payload_for_version) noexcept {
  assert(version < kInvalidVersion);
  assert(capacity_ >= 4);
  if (latest_payload_ == observed_payload_for_version)
    return true;  // No change required

  const uint64_t marked_version =
      observed_payload_for_version.has_payload()
          ? (observed_payload_for_version.version() << 1)
          : (version << 1) | 1;

  if (current_block_size_ < current_block_capacity_ &&
      current_block_size_ != 0) {
    // Attempting to compress the version
    assert(current_block_->first_marked_version_in_block_ < marked_version);
    uint64_t marked_offset =
        marked_version - current_block_->first_marked_version_in_block_;
    if (marked_offset < kInvalidMarkedOffset) {
      if (observed_payload_for_version.has_payload()) {
        PayloadHandle* payloads = current_block_->payloads_;
        payloads[current_block_size_] = observed_payload_for_version.payload();
      }
      uint32_t* marked_offsets = current_block_->marked_offsets_;
      marked_offsets[current_block_size_ - 1] =
          static_cast<uint32_t>(marked_offset);
      ++size_;
      ++current_block_size_;
      latest_payload_ = observed_payload_for_version;
      return true;
    }
    // The marked version can't be compressed into an offset, and has to
    // become the first version of the next block.
    // Filling the offsets with invalid values so that they can be safely
    // skipped later.
    for (uint32_t i = current_block_size_; i < current_block_capacity_; ++i) {
      current_block_->marked_offsets_[i - 1] = kInvalidMarkedOffset;
    }
    size_ = capacity_;
    current_block_size_ = current_block_capacity_;
  }
  if (current_block_size_ == current_block_capacity_) {
    // Allocating a new block
    if (available_blocks_count_ == 0) {
      return false;
    }
    --available_blocks_count_;
    ++stored_data_blocks_count_;
    capacity_ += 5;
    current_block_capacity_ = 5;
    current_block_size_ = 0;
    ++current_block_;
  }
  assert(current_block_size_ == 0 &&
         current_block_size_ < current_block_capacity_);
  current_block_->first_marked_version_in_block_ = marked_version;
  if (observed_payload_for_version.has_payload()) {
    current_block_->payloads_[0] = observed_payload_for_version.payload();
  }
  ++size_;
  ++current_block_size_;
  latest_payload_ = observed_payload_for_version;
  return true;
}

bool SubkeyVersionBlock::Builder::FinalizeAndReserveOne(
    uint64_t version,
    bool has_payload) noexcept {
  const uint32_t optimal_blocks_count = 1 + (size_ * 2) / 5;
  const uint32_t current_blocks_count = (capacity_ + 1) / 5;
  assert(current_blocks_count <= optimal_blocks_count);
  uint32_t extra_blocks_count = optimal_blocks_count - current_blocks_count;
  if (extra_blocks_count > available_blocks_count_) {
    if (available_blocks_count_ == 0) {
      if (current_block_size_ == current_block_capacity_)
        return false;

      assert(current_block_size_ > 0);
      const uint64_t marked_version =
          (version << 1) | static_cast<uint64_t>(!has_payload);
      assert(current_block_->first_marked_version_in_block_ < marked_version);
      return marked_version - current_block_->first_marked_version_in_block_ <
             kInvalidMarkedOffset;
    }
    // Reserving as much as we can.
    extra_blocks_count = available_blocks_count_;
  }
  capacity_ += extra_blocks_count * 5;
  available_blocks_count_ -= extra_blocks_count;
  stored_data_blocks_count_ += extra_blocks_count;
  assert(size_ < capacity_);
  first_block_.capacity_ = capacity_;
  // memory_order_release to publish all non-atomic writes made by the builder.
  // (reader threads will load this with memory_order_acquire first before
  // accessing anything else).
  first_block_.size_.store(size_, std::memory_order_release);
  return true;
}

DataBlockLocation SubkeyVersionBlock::AppendPayloads(
    std::vector<VersionedPayloadHandle>& result) const noexcept {
  if (uint32_t size = size_.load(std::memory_order_relaxed)) {
    const size_t used_blocks_count = 1 + (size) / 5;
    size_t offsets_count = 3;
    for (size_t block_id = 0; block_id < used_blocks_count; ++block_id) {
      const auto& block = this[block_id];
      if ((block.first_marked_version_in_block_ & 1) == 0) {
        result.emplace_back(block.first_marked_version_in_block_ >> 1,
                            block.payloads_[0]);
      }
      --size;
      for (size_t i = 0; size > 0 && i < offsets_count; ++i, --size) {
        if (block.marked_offsets_[i] != kInvalidMarkedOffset) {
          uint64_t marked_version =
              block.first_marked_version_in_block_ + block.marked_offsets_[i];
          if ((marked_version & 1) == 0) {
            result.emplace_back(marked_version >> 1, block.payloads_[i + 1]);
          }
        }
      }
      offsets_count = 4;
    }
  }
  return previous_;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
