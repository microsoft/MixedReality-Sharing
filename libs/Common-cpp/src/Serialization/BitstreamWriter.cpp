// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamWriter.h>

namespace Microsoft::MixedReality::Sharing::Serialization {

void BitstreamWriter::Grow(size_t new_capacity) noexcept {
  assert(capacity_ < new_capacity);
  capacity_ = new_capacity;
  auto new_buffer_ = std::make_unique<uint64_t[]>(capacity_);
  memcpy(new_buffer_.get(), dst_, offset_ * sizeof(uint64_t));
  dst_ = new_buffer_.get();
  external_buffer_ = std::move(new_buffer_);
}

std::string_view BitstreamWriter::Finalize() noexcept {
  size_t size_bytes = offset_ * 8;
  if (temp_bit_offset_) {
    if (offset_ == capacity_)
      Grow(capacity_ + 1);
    dst_[offset_] = temp_;
    size_bytes += (temp_bit_offset_ + 7) / 8;
  }
  return {reinterpret_cast<const char*>(dst_), size_bytes};
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
