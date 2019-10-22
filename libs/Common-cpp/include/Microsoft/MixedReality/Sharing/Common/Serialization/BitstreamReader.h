// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/Platform.h>
#include <Microsoft/MixedReality/Sharing/Common/Serialization/Serialization.h>

#include <cassert>
#include <cstddef>
#include <cstring>
#include <stdexcept>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::Serialization {

class BitstreamReader {
 public:
  explicit BitstreamReader(std::string_view input) noexcept;

  // Reads an exponential-Golomb code (as encoded by BitstreamWriter).
  // Throws std::out_of_range if there is not enough input left
  // (the error also advances the stream to the end, preventing
  // any further non-empty reads).
  uint64_t ReadExponentialGolombCode();

  // Reads up to 32 bits from the bitstream.
  // Throws std::out_of_range if there is not enough input left
  // (the error also advances the stream to the end, preventing
  // any further non-empty reads).
  uint32_t ReadBits32(bit_shift_t bits_count);

  // Reads up to 64 bits from the bitstream.
  // Throws std::out_of_range if there is not enough input left
  // (the error also advances the stream to the end, preventing
  // any further non-empty reads).
  uint64_t ReadBits64(bit_shift_t bits_count);

  size_t untouched_bytes_count() const noexcept {
    return remaining_size_ + read_buf_bits_count_ / 8;
  }

 private:
  void PopulateReadBuf();

  const char* next_;
  size_t remaining_size_;
  size_t read_buf_{0};
  bit_shift_t read_buf_bits_count_{0};
};

MS_MR_SHARING_FORCEINLINE
BitstreamReader::BitstreamReader(std::string_view input) noexcept
    : next_{input.data()}, remaining_size_{input.size()} {}

MS_MR_SHARING_FORCEINLINE
void BitstreamReader::PopulateReadBuf() {
  if (remaining_size_ >= sizeof(read_buf_)) {
    memcpy(&read_buf_, next_, sizeof(read_buf_));
    read_buf_bits_count_ = 8 * sizeof(read_buf_);
    next_ += sizeof(read_buf_);
    remaining_size_ -= sizeof(read_buf_);
  } else if (remaining_size_) {
    read_buf_ = 0;
    memcpy(&read_buf_, next_, remaining_size_);
    read_buf_bits_count_ = 8 * static_cast<bit_shift_t>(remaining_size_);
    next_ += remaining_size_;
    remaining_size_ = 0;
  } else {
    throw std::out_of_range(
        "Can't read bits outside of the input range provided to "
        "BitstreamReader");
  }
}

MS_MR_SHARING_FORCEINLINE
uint32_t BitstreamReader::ReadBits32(bit_shift_t bits_count) {
  assert(bits_count <= 32);
  bit_shift_t appended_bits_count = read_buf_bits_count_;
  uint32_t result = static_cast<uint32_t>(read_buf_);
  while (appended_bits_count < bits_count) {
    PopulateReadBuf();
    result |= static_cast<uint32_t>(read_buf_) << appended_bits_count;
    appended_bits_count += read_buf_bits_count_;
  }
  const bit_shift_t consumed_bits_count =
      read_buf_bits_count_ + bits_count - appended_bits_count;

  if (consumed_bits_count == 8 * sizeof(read_buf_)) {
    read_buf_bits_count_ = 0;
    read_buf_ = 0;
  } else {
    read_buf_bits_count_ -= consumed_bits_count;
    read_buf_ >>= consumed_bits_count;
  }
  return bits_count == 32 ? result : result & ((1ull << bits_count) - 1);
}

MS_MR_SHARING_FORCEINLINE
uint64_t BitstreamReader::ReadBits64(bit_shift_t bits_count) {
  assert(bits_count <= 64);
  bit_shift_t appended_bits_count = read_buf_bits_count_;
  uint64_t result = read_buf_;
  while (appended_bits_count < bits_count) {
    PopulateReadBuf();
    result |= static_cast<uint64_t>(read_buf_) << appended_bits_count;
    appended_bits_count += read_buf_bits_count_;
  }
  const bit_shift_t consumed_bits_count =
      read_buf_bits_count_ + bits_count - appended_bits_count;

  if (consumed_bits_count == 8 * sizeof(read_buf_)) {
    read_buf_bits_count_ = 0;
    read_buf_ = 0;
  } else {
    read_buf_bits_count_ -= consumed_bits_count;
    read_buf_ >>= consumed_bits_count;
  }
  return bits_count == 64 ? result : result & ((1ull << bits_count) - 1);
}

MS_MR_SHARING_FORCEINLINE
uint64_t BitstreamReader::ReadExponentialGolombCode() {
  // The pattern we are looking for is either:
  // * [0..63] '0' bits, then '1', then some value bits
  //   (the same number as the number of zeros, called zeroes_count below).
  // * 64 '0' bits (special case encoding for ~0ull).
  //   In this case we don't check the bits after zeros.

  bit_shift_t zeroes_count = 0;
  bit_shift_t set_bit_position;

#if defined(MS_MR_SHARING_PLATFORM_AMD64)
  while (!_BitScanForward64(&set_bit_position, read_buf_)) {
#elif defined(MS_MR_SHARING_PLATFORM_x86)
  while (!_BitScanForward(&set_bit_position, read_buf_)) {
#else
#error Unsupported platform
#endif
    zeroes_count += read_buf_bits_count_;
    if (zeroes_count >= 64) {
      // Special case for ~0ull,
      // see BitstreamWriter::WriteExponentialGolombCode() for details.
      read_buf_bits_count_ = zeroes_count - 64;
      return ~0ull;
    }
    PopulateReadBuf();
  }
  assert(read_buf_bits_count_ > set_bit_position);
  zeroes_count += set_bit_position;
  if (zeroes_count == 0) {
    // Most common case: consuming a single set bit which encodes a zero.
    --read_buf_bits_count_;
    read_buf_ >>= 1;
    return 0;
  }
  if (zeroes_count >= 64) {
    // Special case for ~0ull,
    // see BitstreamWriter::WriteExponentialGolombCode() for details.
    bit_shift_t consumed_bits_count = set_bit_position + 64 - zeroes_count;
    assert(read_buf_bits_count_ > consumed_bits_count);
    read_buf_bits_count_ -= consumed_bits_count;

#if defined(MS_MR_SHARING_PLATFORM_AMD64)
    read_buf_ >>= consumed_bits_count;
#elif defined(MS_MR_SHARING_PLATFORM_x86)
    if (consumed_bits_count == 32) {
      read_buf_ = 0;
    } else {
      read_buf_ >>= consumed_bits_count;
    }
#endif
    return ~0ull;
  }
  read_buf_bits_count_ -= set_bit_position + 1;
  read_buf_ = (read_buf_ >> 1) >> set_bit_position;

  bit_shift_t appended_bits_count = read_buf_bits_count_;
  uint64_t result = read_buf_;
  while (appended_bits_count < zeroes_count) {
    PopulateReadBuf();
    result |= static_cast<uint64_t>(read_buf_) << appended_bits_count;
    appended_bits_count += read_buf_bits_count_;
  }
  const bit_shift_t consumed_bits_count =
      read_buf_bits_count_ + zeroes_count - appended_bits_count;
  read_buf_bits_count_ -= consumed_bits_count;
#if defined(MS_MR_SHARING_PLATFORM_AMD64)
  read_buf_ >>= consumed_bits_count;
#elif defined(MS_MR_SHARING_PLATFORM_x86)
  if (consumed_bits_count == 32) {
    read_buf_ = 0;
  } else {
    read_buf_ >>= consumed_bits_count;
  }
#endif
  const uint64_t mask = (1ull << zeroes_count) - 1;
  return (result & mask) + mask;
}  // namespace Microsoft::MixedReality::Sharing::Serialization

}  // namespace Microsoft::MixedReality::Sharing::Serialization
