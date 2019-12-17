// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/Serialization/Serialization.h>

#include <cassert>
#include <cstddef>
#include <cstring>
#include <memory>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::Serialization {

class BitstreamWriter {
 public:
  // Outputs the provided bits into the stream.
  // Expects that the provided value fits into bits_count bits,
  // otherwise the behavior is undefined.
  void WriteBits(uint64_t value, bit_shift_t bits_count) noexcept;

  // Encodes the provided value as an order-0 exponential-Golomb code
  // (Little-Endian variation that stores zeros in low bits).
  // Has a special shortened encoding for ~0ull since we are not interested
  // in arbitrarily large codes.
  void WriteExponentialGolombCode(uint64_t value) noexcept;

  // Flushes the buffer and returns the view of it.
  // The stream is extended with '0' bits to become byte-aligned.
  std::string_view Finalize() noexcept;

 private:
  void Grow(size_t new_capacity) noexcept;

  // Writes a single '1' bit into the stream.
  MS_MR_SHARING_FORCEINLINE void WriteOneBit() noexcept;

  static constexpr size_t kInplaceElementsCount = 128;
  uint64_t inplace_buffer_[kInplaceElementsCount];
  uint64_t* dst_{inplace_buffer_};
  uint64_t temp_{0};
  bit_shift_t temp_bit_offset_{0};
  size_t offset_{0};
  size_t capacity_{kInplaceElementsCount};
  std::unique_ptr<uint64_t[]> external_buffer_;
};

MS_MR_SHARING_FORCEINLINE
void BitstreamWriter::WriteOneBit() noexcept {
  if (temp_bit_offset_ == 63) {
    if (offset_ == capacity_)
      Grow(capacity_ * 2);
    dst_[offset_++] = temp_ | (1ull << temp_bit_offset_);
    temp_bit_offset_ = 0;
    temp_ = 0;
  } else {
    temp_ |= 1ull << temp_bit_offset_;
    ++temp_bit_offset_;
  }
}

MS_MR_SHARING_FORCEINLINE
void BitstreamWriter::WriteBits(uint64_t value,
                                bit_shift_t bits_count) noexcept {
  assert((bits_count == 64) || (bits_count < 64 && (value >> bits_count) == 0));
  temp_ |= value << temp_bit_offset_;
  temp_bit_offset_ += bits_count;
  if (temp_bit_offset_ > 63) {
    if (offset_ == capacity_)
      Grow(capacity_ * 2);
    dst_[offset_++] = temp_;
    temp_bit_offset_ &= 63;
    auto shift = bits_count - temp_bit_offset_;
    temp_ = shift == 64 ? 0 : value >> shift;
  }
}

MS_MR_SHARING_FORCEINLINE
void BitstreamWriter::WriteExponentialGolombCode(uint64_t value) noexcept {
  // This is a Little-Endian version of the encoding.
  // First, making the value non-zero by adding 1 and special-casing
  // the ~0ull case. Then for the input that looks like:
  //   1xx..xx
  // we push N zeros into the stream, then '1', then xx..xx bits.
  // N is equal to the number of bits in the xx..xx payload.
  // Then, when reading the value later, we'll count the number of 0s in the
  // stream before the separating '1' and reverse the transform
  // (add (1ull << N) - 1) to get the original value before we offsetted it
  // by 1 and chopped off the leading '1'.
  if (value == 0) {  // Fast path for the most common case
    WriteOneBit();
  } else {
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
      // We could encode this as a 65-bit value (exponential-Golomb coding
      // doesn't have an upper limit for the size of the value) by writing 64
      // zeros, one, and then 64 zeros again, but since this is the only value
      // like this, we can skip the long tail and just write 64 zeros.
      WriteBits(0, 64);
      return;
    }
    uint64_t pattern = 1ull << index;
    bit_shift_t pattern_bits_count = index + 1;
    WriteBits(pattern, pattern_bits_count);
    WriteBits(value & (pattern - 1), index);
  }
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
