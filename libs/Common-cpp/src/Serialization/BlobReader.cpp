// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BlobReader.h>

namespace Microsoft::MixedReality::Sharing::Serialization {

MS_MR_SHARING_FORCEINLINE
void BlobReader::PopulateReadBuf() {
  if (unread_bytes_count_ >= sizeof(bit_buf_)) {
    unread_bytes_count_ -= sizeof(bit_buf_);
    memcpy(&bit_buf_, unread_bytes_begin_ + unread_bytes_count_,
           sizeof(bit_buf_));
    bit_buf_bits_count_ = kBitBufferBitsCount;
    return;
  }
  if (unread_bytes_count_ != 0) {
    bit_buf_ = 0;
    memcpy(reinterpret_cast<std::byte*>(&bit_buf_) + sizeof(bit_buf_) -
               unread_bytes_count_,
           unread_bytes_begin_, unread_bytes_count_);
    bit_buf_bits_count_ = static_cast<bit_shift_t>(unread_bytes_count_) * 8;
    unread_bytes_count_ = 0;
    return;
  }
  throw std::out_of_range("Not enough bytes in the blob");
}

MS_MR_SHARING_FORCEINLINE
void BlobReader::PopulateReadBuf(bit_shift_t min_bits_count) {
  assert(min_bits_count <= kBitBufferBitsCount);
  if (unread_bytes_count_ >= sizeof(bit_buf_)) {
    unread_bytes_count_ -= sizeof(bit_buf_);
    memcpy(&bit_buf_, unread_bytes_begin_ + unread_bytes_count_,
           sizeof(bit_buf_));
    bit_buf_bits_count_ = kBitBufferBitsCount;
    return;
  }
  if (unread_bytes_count_ * 8 >= min_bits_count) {
    bit_buf_ = 0;
    memcpy(reinterpret_cast<std::byte*>(&bit_buf_) + sizeof(bit_buf_) -
               unread_bytes_count_,
           unread_bytes_begin_, unread_bytes_count_);
    bit_buf_bits_count_ = static_cast<bit_shift_t>(unread_bytes_count_) * 8;
    unread_bytes_count_ = 0;
    return;
  }
  throw std::out_of_range("Not enough bytes in the blob");
}

std::string_view BlobReader::ReadBytes(size_t bytes_count) {
  const char* begin = unread_bytes_begin_;
  if (bytes_count <= unread_bytes_count_) {
    unread_bytes_count_ -= bytes_count;
    unread_bytes_begin_ += bytes_count;
  } else if (bytes_count <= unread_bytes_count_ + bit_buf_bits_count_ / 8) {
    // Can steal some bytes from the bit buffer.
    auto borrowed_bits_count =
        static_cast<bit_shift_t>(bytes_count - unread_bytes_count_) * 8;
    unread_bytes_count_ = 0;
    bit_buf_bits_count_ -= borrowed_bits_count;
    // Clearing out borrowed bytes.
    if (bit_buf_bits_count_ == 0) {
      bit_buf_ = 0;
    } else {
      bit_buf_ &=
          ~(static_cast<decltype(bit_buf_)>(~0ull) >> bit_buf_bits_count_);
    }
  } else {
    throw std::out_of_range("Not enough bytes in the blob");
  }
  return {begin, bytes_count};
}

template <typename T>
MS_MR_SHARING_FORCEINLINE T
BlobReader::ReadWithSingleFetch(bit_shift_t bits_count) {
  assert(bits_count <= kBitBufferBitsCount &&
         bits_count >= bit_buf_bits_count_);
  bit_shift_t shift = kBitBufferBitsCount - bits_count;
  size_t result = 0;
  if (bit_buf_bits_count_) {
    result = bit_buf_ >> shift;
    bits_count -= bit_buf_bits_count_;
    shift += bit_buf_bits_count_;
    PopulateReadBuf(bits_count);
  } else {
    PopulateReadBuf(bits_count);
    if (bits_count == kBitBufferBitsCount) {
      result = bit_buf_;
      bit_buf_ = 0;
      bit_buf_bits_count_ = 0;
      return static_cast<T>(result);
    }
  }
  result |= bit_buf_ >> shift;
  bit_buf_bits_count_ -= bits_count;
  assert(bits_count < kBitBufferBitsCount);
  bit_buf_ <<= bits_count;
  return static_cast<T>(result);
}

template <typename T>
MS_MR_SHARING_FORCEINLINE T BlobReader::ReadBits(bit_shift_t bits_count) {
  assert((bits_count > 0) && (bits_count <= 8 * sizeof(T)));
  if (bits_count < bit_buf_bits_count_) {
    // Fast path: the most common case
    // Note that the case where bits_count == bit_buf_bits_count_ is excluded.
    // While it is similar (all the bits are in bit_buf_), it would require an
    // extra check to make sure that <<= below is valid.
    T result = static_cast<T>(bit_buf_ >> (kBitBufferBitsCount - bits_count));
    bit_buf_ <<= bits_count;
    bit_buf_bits_count_ -= bits_count;
    return result;
  }
  if (bits_count == bit_buf_bits_count_) {
    T result = static_cast<T>(bit_buf_ >> (kBitBufferBitsCount - bits_count));
    bit_buf_ = 0;
    bit_buf_bits_count_ = 0;
    return result;
  }
  if constexpr (sizeof(bit_buf_) >= sizeof(T)) {
    return ReadWithSingleFetch<T>(bits_count);
  } else {
    // This is the only case where we may need to fetch the data several times.
    static_assert(sizeof(T) == 8 && sizeof(bit_buf_) == 4);
    if (bits_count <= 32) {
      return ReadWithSingleFetch<T>(bits_count);
    }
    // This will consume the entire bit_buf_ and perform some
    // additional reads after that.
    T result = 0;
    if (bit_buf_bits_count_ != 0) {
      assert(bits_count > 32);
      result = static_cast<T>(bit_buf_) << (bits_count - 32);
      bits_count -= bit_buf_bits_count_;
      bit_buf_bits_count_ = 0;
      if (bits_count <= 32) {
        return result | ReadWithSingleFetch<T>(bits_count);
      }
    }
    PopulateReadBuf();
    assert(bits_count > 32 && bit_buf_bits_count_ != 0);
    result |= static_cast<T>(bit_buf_) << (bits_count - 32);
    bits_count -= bit_buf_bits_count_;
    PopulateReadBuf(bits_count);
    assert(bits_count > 0);
    result |= static_cast<T>(bit_buf_ >> (32 - bits_count));
    if (bits_count != 32) {
      bit_buf_ <<= bits_count;
      bit_buf_bits_count_ -= bits_count;
    } else {
      bit_buf_ = 0;
      bit_buf_bits_count_ = 0;
    }
    return result;
  }
}

uint32_t BlobReader::ReadBits32(bit_shift_t bits_count) {
  return ReadBits<uint32_t>(bits_count);
}

uint64_t BlobReader::ReadBits64(bit_shift_t bits_count) {
  return ReadBits<uint64_t>(bits_count);
}

uint64_t BlobReader::ReadExponentialGolombCode() {
  // Counting the number of zero bits to determine the length of the code.
  // 64 zeros is a special case for ~0ull (see BlobWriter for details).
  bit_shift_t zeroes_count = 0;
  bit_shift_t set_bit_position;

#if defined(MS_MR_SHARING_PLATFORM_AMD64)
  while (!_BitScanReverse64(&set_bit_position, bit_buf_))
#elif defined(MS_MR_SHARING_PLATFORM_x86)
  while (!_BitScanReverse(&set_bit_position, bit_buf_))
#else
#error Unsupported platform
#endif
  {
    zeroes_count += bit_buf_bits_count_;
    if (zeroes_count >= 64) {
      // Special case for ~0ull (see BlobWriter for details).
      bit_buf_bits_count_ = zeroes_count - 64;
      return ~0ull;
    }
    PopulateReadBuf();
  }
  static constexpr bit_shift_t kLastBitPosition = kBitBufferBitsCount - 1;
  const bit_shift_t new_zeros_count = kLastBitPosition - set_bit_position;
  zeroes_count += new_zeros_count;
  if (zeroes_count >= 64) {
    // Special case for ~0ull (see BlobWriter for details).
    const bit_shift_t consumed_zeros_count =
        new_zeros_count - zeroes_count + 64;
    bit_buf_bits_count_ -= consumed_zeros_count;
    bit_buf_ <<= consumed_zeros_count;
    return ~0ull;
  }
  bit_buf_bits_count_ -= new_zeros_count;
  bit_buf_ <<= new_zeros_count;
  return ReadBits<uint64_t>(zeroes_count + 1) - 1;
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
