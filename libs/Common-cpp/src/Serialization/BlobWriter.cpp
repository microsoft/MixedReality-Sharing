// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BlobWriter.h>

#include <algorithm>

namespace Microsoft::MixedReality::Sharing::Serialization {

void BlobWriter::Grow(size_t min_free_bytes_after_grow) noexcept {
  // Layout before the call:
  // 1010101010...(free_bytes_count_)...[8 reserved bytes]101010101010101010_
  // ^         ^                                          ^                 ^
  // |         |                                          |                 |
  // buffer_   bytes_scection_end_      bits_section_begin_        buffer_end
  //
  // The new buffer will have the same layout, but the free section in the
  // middle (free_bytes_count_) will be larger.

  const size_t bytes_size = bytes_section_size();
  const size_t bits_size = bits_section_size();
  const size_t buffer_size = reinterpret_cast<std::byte*>(buffer_end_) -
                             reinterpret_cast<std::byte*>(buffer_);

  // 15 comes from 8 (to always have free space for the current bit buffer) and
  // 7 (to make the division round up).
  const size_t min_new_elements_count =
      (bytes_size + bits_size + min_free_bytes_after_grow + 15) / 8;
  const size_t new_elements_count =
      std::max(min_new_elements_count, buffer_size / 4);
  const size_t new_buffer_size = new_elements_count * 8;
  auto new_buffer = std::make_unique<uint64_t[]>(new_elements_count);
  std::byte* new_buffer_begin = reinterpret_cast<std::byte*>(new_buffer.get());
  std::byte* new_buffer_end = new_buffer_begin + new_buffer_size;

  // Copying the bytes section (head)
  memcpy(new_buffer_begin, buffer_, bytes_size);
  bytes_scection_end_ = new_buffer_begin + bytes_size;

  // Copying the bits section (tail)
  std::byte* new_bits_section_begin = new_buffer_end - bits_size;
  memcpy(new_bits_section_begin, bits_section_begin_, bits_size);
  bits_section_begin_ = reinterpret_cast<uint64_t*>(new_bits_section_begin);
  // Reserving 8 bytes for the current bit buffer.
  free_bytes_count_ = new_buffer_size - bytes_size - bits_size - 8;
  if (buffer_ != inplace_buffer_)
    delete[] buffer_;
  buffer_ = new_buffer.release();
  buffer_end_ = buffer_ + new_elements_count;
}

void BlobWriter::WriteGolomb(uint64_t value) noexcept {
  // The encoding increments the value by 1 and counts the number of bits in the
  // result. Then it writes the number of zeros equal to the number of bits
  // minus one, and then all the significant bits of the incremented number.
  // For example, a binary value 11101 (29, MSB first) will be:
  // * incremented to 11101 (30)
  // * preceded by 4 zeros: 000011101 (30)
  // Since the bits in BlobWriter are written from the end, the reader
  // will first count the number of leading zeros to determine the total number
  // of bits after them.
  // We special-case the number ~0ull (that would normally overflow after the
  // increment, and without the register size limitation would normally produce
  // 64 zeros, followed by 1, followed by 64 zeros).
  // Since no other inputs would produce a prefix of 64 zeros, that's what we
  // use to encode ~0ull.
  if (value == 0) {   // Fast path for the most common case
    WriteBits(1, 1);  // TODO: optimize further
    return;
  }
  // Offsetting the value by 1. This can overflow if the value was ~0ull.
  value += 1;
  bit_shift_t index;
#if defined(MS_MR_SHARING_PLATFORM_AMD64)
  if (_BitScanReverse64(&index, value)) {
    // Do nothing
#elif defined(MS_MR_SHARING_PLATFORM_x86)
  if (_BitScanReverse(&index, (value >> 32))) {
    index += 32;
  } else if (_BitScanReverse(&index, static_cast<uint32_t>(value))) {
    // Do nothing
#endif
  } else {
    // Special encoding for ~0ull. value is 0 here due to the overflow.
    // All other values will encode themselves with less than 64 leading zeros.
    WriteBits(0, 64);
    return;
  }
  WriteBits(0, index);
  WriteBits(value, index + 1);
}

void BlobWriter::WriteBytesWithSize(const std::byte* data,
                                    size_t size) noexcept {
  // Not reusing WriteBytes() to avoid the possibility of double reallocation
  // (growing with 16 extra bytes ensures that we'll always be able to write
  // the size as exponential-Golomb code).
  if (free_bytes_count_ < size)
    Grow(size + 16);
  memcpy(bytes_scection_end_, data, size);
  bytes_scection_end_ += size;
  free_bytes_count_ -= size;
  WriteGolomb(size);
}

void BlobWriter::WritePresentOptionalGolomb(uint64_t present_value) noexcept {
  if (present_value >= ~1ull) {
    // Special-casing these two values, so that they are always encoded with
    // 65 bits.
    WriteBits(0, 64);
    WriteBits(~present_value, 1);
  } else {
    WriteGolomb(present_value + 1);
  }
}

std::string_view BlobWriter::Finalize() noexcept {
  const size_t bits_size = bits_section_size();
  const size_t bytes_size = bytes_section_size();
  const size_t pending_size = pending_bits_size();
  const auto result_size = bits_size + bytes_size + pending_size;

  // TODO: optimize for the case when it's faster to move bytes instead
  // if (bytes_size >= bits_size) {

  // Saving the bit buffer if necessary
  if (pending_size) {
    // Rounding up so that even 1 bit would produce a full byte.
    const std::byte* src =
        reinterpret_cast<const std::byte*>(&bit_buffer_) + 8 - pending_size;
    memcpy(bytes_scection_end_, src, pending_size);
    bytes_scection_end_ += pending_size;
  }
  memmove(bytes_scection_end_, bits_section_begin_, bits_size);
  return {reinterpret_cast<const char*>(buffer_), result_size};
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
