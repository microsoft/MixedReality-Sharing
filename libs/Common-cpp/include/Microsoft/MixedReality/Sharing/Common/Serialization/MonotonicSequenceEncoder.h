// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cstdint>

namespace Microsoft::MixedReality::Sharing::Serialization {

class BitstreamReader;
class BitstreamWriter;

// Helper class for encoding and decoding monotonic sequences of numbers in a
// space-efficient way.

// FIXME: this is a temporary implementation that doesn't do a very good job
// with compressing the sequence. We can do way better than this.
class MonotonicSequenceEncoder {
 public:
  // Encodes the next value of the monotonic sequence using the prediction based
  // on values observed so far. The provided value must be greater than any of
  // the previous values used with this encoder.
  void EncodeNext(uint64_t value, BitstreamWriter& writer);

  // Decodes the next value of the monotonic sequence using the prediction based
  // on values observed so far. Returned values will always be ordered in
  // ascending order.
  uint64_t DecodeNext(BitstreamReader& reader);

 private:
  uint64_t predicted_next_value_{0};
  bool can_continue_{true};
};

}  // namespace Microsoft::MixedReality::Sharing::Serialization
