// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/Serialization/Serialization.h>

#include <algorithm>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::Serialization {

// Reads blobs produced by BlobWriter, that consist of a concatenated byte
// stream and a reversed bit stream. See BlobWriter for the details.
class BlobReader {
 public:
  explicit BlobReader(std::string_view input) noexcept;

  // Reads next bytes_count bytes (as encoded by BlobWriter).
  // Throws std::out_of_range if there is not enough input left.
  // The behavior is undefined if the reader is reused after it
  // had thrown an exception.
  std::string_view ReadBytes(size_t bytes_count);

  // Reads up to 32 bits from the bitstream.
  // Throws std::out_of_range if there is not enough input left.
  // The behavior is undefined if the reader is reused after it
  // had thrown an exception, or if bits_count is not in [1, 32].
  uint32_t ReadBits32(bit_shift_t bits_count);

  // Reads up to 64 bits from the bitstream.
  // Throws std::out_of_range if there is not enough input left.
  // The behavior is undefined if the reader is reused after it
  // had thrown an exception, or if bits_count is not in [1, 64].
  uint64_t ReadBits64(bit_shift_t bits_count);

  // Reads an exponential-Golomb code (as encoded by BlobWriter).
  // Throws std::out_of_range if there is not enough input left.
  // The behavior is undefined if the reader is reused after it
  // had thrown an exception.
  uint64_t ReadExponentialGolombCode();

  // Returns true if there are no more than 7 unread bits,
  // and all of them are 0.
  // The reader can't tell the difference between zero padding
  // (in case where the number of written bits wasn't divisible by 8)
  // and actual data bits that are 0, therefore "probably".
  // This method should only be used as an integrity check after the reading,
  // is done, not as an indication for when to stop reading.
  bool ProbablyNoMoreData() const noexcept;

 private:
  template <typename T>
  T ReadBits(bit_shift_t bits_count);

  template <typename T>
  T ReadWithSingleFetch(bit_shift_t bits_count);

  void PopulateReadBuf();
  void PopulateReadBuf(bit_shift_t min_bits_count);

  const char* unread_bytes_begin_;
  size_t unread_bytes_count_;

  size_t bit_buf_{0};
  bit_shift_t bit_buf_bits_count_{0};
  static constexpr bit_shift_t kBitBufferBitsCount = 8 * sizeof(bit_buf_);
};

MS_MR_SHARING_FORCEINLINE
BlobReader::BlobReader(std::string_view input) noexcept
    : unread_bytes_begin_{input.data()}, unread_bytes_count_{input.size()} {}

MS_MR_SHARING_FORCEINLINE
bool BlobReader::ProbablyNoMoreData() const noexcept {
  return unread_bytes_count_ == 0 && bit_buf_bits_count_ < 8 && bit_buf_ == 0;
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
