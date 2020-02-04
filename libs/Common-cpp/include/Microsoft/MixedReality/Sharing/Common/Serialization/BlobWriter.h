// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/Serialization/Serialization.h>

#include <cstddef>
#include <optional>

namespace Microsoft::MixedReality::Sharing::Serialization {

// Builds a contiguous blob with a byte stream at the beginning
// and a bit stream at the end.
// It is expected that the produced blob will be read in the same order as it
// was written, and eventually the two streams will meet in the middle of the
// blob.
// For example, for the following sequence of writes
//   1. bytes1
//   2. bytes2
//   3. bits1
//   4. bytes3
//   5. bits2
// the resulting blob will look like:
// [bytes1][bytes2][bytes3][bits2][bits1]
//                              ^
// Note that bit stream is effectively written from the tail. BlobReader will
// also read bits from the tail, so bits1 will be returned before bits2.
// Since it is initially unknown how large the resulting blob is going to be,
// the blob should be finalized after all writing is done.
class BlobWriter {
 public:
  ~BlobWriter() {
    if (buffer_ != inplace_buffer_)
      delete[] buffer_;
  }

  // Appends the provided bytes to the byte stream.
  void WriteBytes(const std::byte* data, size_t size) noexcept;
  void WriteBytes(const char* data, size_t size) noexcept;
  void WriteBytes(std::string_view sv) noexcept;

  // Writes the provided bytes to the byte stream and their count
  // as exponential-Golomb code.
  // The reader will be able to read this string without knowing
  // its size in advance, see BlobReader::ReadBytesWithSize().
  void WriteBytesWithSize(const std::byte* data, size_t size) noexcept;
  void WriteBytesWithSize(const char* data, size_t size) noexcept;
  void WriteBytesWithSize(std::string_view sv) noexcept;

  // Appends the provided bits to the bit stream.
  // Expects that the provided value fits into bits_count bits, otherwise the
  // behavior is undefined.
  void WriteBits(uint64_t value, bit_shift_t bits_count) noexcept;

  // Writes a bool as a single bit.
  void WriteBool(bool value) noexcept;

  // Encodes the provided value as an order-0 exponential-Golomb code.
  // Has a special shortened encoding for ~0ull since we are not interested
  // in arbitrarily large codes.
  void WriteGolomb(uint64_t value) noexcept;

  // Encodes the provided optional value as an order-0 exponential-Golomb code,
  // offsetted so that the empty optional has the shortest possible encoding
  // (1 bit) and all other values are encoded as a value that is 1 greater
  // (so, for example, 5 will be encoded as 6).
  // Has a special shortened encoding for ~0ull and ~1ull, so the offsetting
  // procedure above doesn't overflow.
  void WriteOptionalGolomb(
      const std::optional<uint64_t> optional_value) noexcept;

  // Equivalent to WriteOptionalGolomb with optional_value
  // argument equal to present_value.
  void WritePresentOptionalGolomb(uint64_t present_value) noexcept;

  // Equivalent to WriteOptionalGolomb with optional_value
  // argument being empty.
  void WriteMissingOptionalGolomb() noexcept;

  // The number of bytes the blob will occupy if the writer would be finalized
  // with the current state.
  size_t finalized_size() const noexcept {
    return bytes_section_size() + bits_section_size() + pending_bits_size();
  }

  // Composes the final contiguous blob inside the internal buffers.
  // The behavior of any other methods of this object after Finalize() was
  // called is undefined.
  std::string_view Finalize() noexcept;

  // TODO: add Finalize(dst) that copies the data to a preallocated buffer
  // instead of finalizing in place.

 private:
  void Grow(size_t min_free_bytes_after_grow) noexcept;

  size_t bytes_section_size() const noexcept {
    return bytes_scection_end_ - reinterpret_cast<std::byte*>(buffer_);
  }

  size_t bits_section_size() const noexcept {
    return reinterpret_cast<std::byte*>(buffer_end_) -
           reinterpret_cast<std::byte*>(bits_section_begin_);
  }

  size_t pending_bits_size() const noexcept {
    // Rounds the number of bytes up
    // (we'll write the byte even if only one bit of it is occupied).
    return (71 - free_bits_count_) / 8;
  }

  static constexpr size_t kInplaceElementsCount = 128;
  uint64_t inplace_buffer_[kInplaceElementsCount];  // Stays uninitialized
  std::byte* bytes_scection_end_{reinterpret_cast<std::byte*>(inplace_buffer_)};

  // The last 8 bytes are reserved for bit_buffer_, so that we can always write
  // bit_buffer_ in Finalize() without a reallocation.
  size_t free_bytes_count_{sizeof(inplace_buffer_) - sizeof(uint64_t)};

  uint64_t* bits_section_begin_{inplace_buffer_ + kInplaceElementsCount};

  // When it runs out of bits, the content is appended before
  // bits_section_head_, and the bits section is extended.
  uint64_t bit_buffer_{0};
  bit_shift_t free_bits_count_{64};

  uint64_t* buffer_ = inplace_buffer_;
  uint64_t* buffer_end_ = inplace_buffer_ + kInplaceElementsCount;
};

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBytes(const std::byte* data, size_t size) noexcept {
  if (free_bytes_count_ < size)
    Grow(size);
  memcpy(bytes_scection_end_, data, size);
  bytes_scection_end_ += size;
  free_bytes_count_ -= size;
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBytes(const char* data, size_t size) noexcept {
  WriteBytes(reinterpret_cast<const std::byte*>(data), size);
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBytes(std::string_view sv) noexcept {
  WriteBytes(reinterpret_cast<const std::byte*>(sv.data()), sv.size());
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBits(uint64_t value, bit_shift_t bits_count) noexcept {
  assert((bits_count == 64) || (bits_count < 64 && (value >> bits_count) == 0));
  if (bits_count > free_bits_count_) {
    if (free_bytes_count_ < 8)
      Grow(8);
    uint64_t to_write = bit_buffer_;
    if (free_bits_count_ != 0) {
      bits_count -= free_bits_count_;
      to_write |= value >> bits_count;
    }
    --bits_section_begin_;
    *bits_section_begin_ = to_write;
    free_bytes_count_ -= 8;
    free_bits_count_ = 64 - bits_count;
    bit_buffer_ = value << free_bits_count_;
  } else {
    free_bits_count_ -= bits_count;
    bit_buffer_ |= value << free_bits_count_;
  }
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBytesWithSize(const char* data, size_t size) noexcept {
  WriteBytesWithSize(reinterpret_cast<const std::byte*>(data), size);
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBytesWithSize(std::string_view sv) noexcept {
  WriteBytesWithSize(reinterpret_cast<const std::byte*>(sv.data()), sv.size());
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteMissingOptionalGolomb() noexcept {
  WriteBits(1, 1);
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteOptionalGolomb(
    const std::optional<uint64_t> optional_value) noexcept {
  if (optional_value) {
    WritePresentOptionalGolomb(*optional_value);
  } else {
    WriteMissingOptionalGolomb();
  }
}

MS_MR_SHARING_FORCEINLINE
void BlobWriter::WriteBool(bool value) noexcept {
  WriteBits(value, 1);
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
