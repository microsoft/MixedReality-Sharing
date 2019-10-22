// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/MonotonicSequenceEncoder.h>

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamReader.h>
#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamWriter.h>

namespace Microsoft::MixedReality::Sharing::Serialization {

void MonotonicSequenceEncoder::EncodeNext(uint64_t value,
                                          BitstreamWriter& writer) {
  if (value < predicted_next_value_) {
    throw std::invalid_argument{
        "Can't encode a monotonic sequence: each value must be strictly "
        "greater than the previous one"};
  }
  uint64_t diff = value - predicted_next_value_;
  writer.WriteExponentialGolombCode(diff);
  if (value == ~0ull) {
    can_continue_ = false;
  } else {
    predicted_next_value_ = value + 1;
  }
}

uint64_t MonotonicSequenceEncoder::DecodeNext(BitstreamReader& reader) {
  if (!can_continue_) {
    throw std::invalid_argument{
        "Can't decode the next value of the monotonic sequence because the "
        "largest encodable value is already reached"};
  }
  uint64_t diff = reader.ReadExponentialGolombCode();
  if (diff > ~0ull - predicted_next_value_) {
    throw std::invalid_argument{
        "Can't decode the next value of the monotonic sequence: value "
        "overflows the maximum encodable value"};
  }
  uint64_t decoded = predicted_next_value_ + diff;
  if (decoded == ~0ull) {
    can_continue_ = false;
  } else {
    predicted_next_value_ = decoded + 1;
  }
  return decoded;
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
