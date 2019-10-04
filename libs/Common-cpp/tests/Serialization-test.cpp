// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/Common/BitstreamReader.h>
#include <Microsoft/MixedReality/Sharing/Common/BitstreamWriter.h>

namespace Microsoft::MixedReality::Sharing::Serialization {
using namespace std::literals;

namespace {
struct EncodedValue {
  uint64_t value;
  bit_shift_t max_width_bits;  // Can be larger than the actual width
};

// Generates a test set of values of different bit "widths" (the size of the
// significant part up to the highest non-zero bit). Compared to uniform
// distribution, values of smaller widths are a lot more likely (and each width
// is about as likely as any other). Two special values (0 and ~0ull) are also
// inserted with much increased probability since they are special-cased in the
// stream code and should be properly tested.
std::vector<EncodedValue> GenerateRandomValues(size_t count) {
  std::uniform_int_distribution<uint64_t> uint64_distribution{0, ~0ull};

  // 64 and 65 are treated specially
  std::uniform_int_distribution<bit_shift_t> shift_distribution{0, 65};
  std::mt19937 rng{std::random_device{}()};
  std::vector<EncodedValue> values(count);
  for (auto& pair : values) {
    auto shift = shift_distribution(rng);
    // Two special values for special cased numbers.
    if (shift == 64) {
      pair.value = 0;
      pair.max_width_bits = 0;
    } else if (shift == 65) {
      pair.value = ~0ull;  // Has special representation in our Golomb encoding.
      pair.max_width_bits = 64;
    } else {
      // This can produce smaller numbers than expected (if top bits just
      // happened to be zero), but this is fine.
      pair.value = uint64_distribution(rng) >> shift;
      pair.max_width_bits = 64 - shift;
    }
  }
  return values;
}
}  // namespace

TEST(Serialization, bitstream_write_read_bits) {
  // Writes and reads fixed sized patterns of ones and zeros.
  static constexpr size_t kRepeatsCount = 10;
  for (bit_shift_t offset = 0; offset < 65; ++offset) {
    for (bit_shift_t shift = 0; shift < 64; ++shift) {
      BitstreamWriter writer;
      uint64_t value = (1ull << shift) - 1;
      for (int i = 0; i < kRepeatsCount; ++i) {
        writer.WriteBits(0, offset);
        writer.WriteBits(value, shift);
      }
      BitstreamReader reader{writer.Finalize()};
      for (int i = 0; i < kRepeatsCount; ++i) {
        ASSERT_EQ(reader.ReadBits64(offset), 0);
        ASSERT_EQ(reader.ReadBits64(shift), value);
      }
    }
  }
}

TEST(Serialization, bitstream_write_read_bits_random) {
  // Writes and reads random values of various sizes.
  auto values = GenerateRandomValues(20000);
  BitstreamWriter writer;
  for (const auto& x : values) {
    writer.WriteBits(x.value, x.max_width_bits);
  }
  BitstreamReader reader{writer.Finalize()};
  for (const auto& expected : values) {
    uint64_t read_value = reader.ReadBits64(expected.max_width_bits);
    ASSERT_EQ(expected.value, read_value);
  }
}

TEST(Serialization, bitstream_write_read_golomb_codes_short_sequences) {
  // Writing and reading short sequences of exponential-Golomb codes,
  // written from various offsets.
  static constexpr size_t kRerunsCount = 10;
  for (size_t i = 0; i < kRerunsCount; ++i) {
    for (size_t sequence_length = 1; sequence_length < 10; ++sequence_length) {
      for (bit_shift_t offset = 0; offset < 65; ++offset) {
        auto values = GenerateRandomValues(sequence_length);
        BitstreamWriter writer;
        writer.WriteBits(0, offset);
        for (const auto& x : values) {
          writer.WriteExponentialGolombCode(x.value);
        }
        BitstreamReader reader{writer.Finalize()};
        ASSERT_EQ(reader.ReadBits64(offset), 0);
        for (const auto& expected : values) {
          uint64_t read_value = reader.ReadExponentialGolombCode();
          ASSERT_EQ(expected.value, read_value);
        }
      }
    }
  }
}

TEST(Serialization, bitstream_write_read_golomb_codes_long_sequence) {
  // Writes and reads multiple exponential-Golomb codes to the stream.
  auto values = GenerateRandomValues(20000);
  BitstreamWriter writer;
  for (const auto& x : values) {
    writer.WriteExponentialGolombCode(x.value);
  }
  BitstreamReader reader{writer.Finalize()};
  for (const auto& expected : values) {
    uint64_t read_value = reader.ReadExponentialGolombCode();
    ASSERT_EQ(expected.value, read_value);
  }
}

TEST(Serialization, read_from_empty) {
  // Reads from an empty bitstream.
  {
    BitstreamReader reader{{}};
    for (size_t i = 0; i < 1000; ++i)
      ASSERT_EQ(reader.ReadBits64(0), 0);
  }

  for (bit_shift_t width = 1; width <= 64; ++width) {
    BitstreamReader reader{{}};
    ASSERT_THROW(reader.ReadBits64(width), std::out_of_range);
  }

  BitstreamReader reader{{}};
  ASSERT_THROW(reader.ReadExponentialGolombCode(), std::out_of_range);
}

TEST(Serialization, read_out_of_range) {
  BitstreamWriter writer;
  writer.WriteBits(0xFF, 8);
  auto sv = writer.Finalize();
  ASSERT_EQ(sv, "\xff"sv);
  BitstreamReader reader{sv};
  ASSERT_EQ(reader.ReadBits64(4), 0xF);
  ASSERT_EQ(reader.ReadBits64(4), 0xF);

  // Can read 0-sized inputs
  for (int i = 0; i < 10; ++i)
    ASSERT_EQ(reader.ReadBits64(0), 0x0);

  // Can't read anything else
  ASSERT_THROW(reader.ReadBits64(1), std::out_of_range);
}

TEST(Serialization, read_golomb_out_of_range) {
  BitstreamWriter writer;
  writer.WriteExponentialGolombCode(6);
  writer.WriteExponentialGolombCode(2);
  auto sv = writer.Finalize();

  // 110 | 11100
  // ||    ||| ^ Low bit
  // ||    ||^ Separator bit, the number of trailing zeros is 2
  // ||    ^^ Payload of the first number. We attach a leading 1 (to get 111)
  // ||       and subtract 1 to get the value 6.
  // |^ Separator of the next number. 1 trailing zero before it.
  // ^ Payload of the second number Attaching a leading 1 (to get 11)
  //   and subtracting 1 to get the value 2.

  ASSERT_EQ(sv, "\xDC"sv);
  BitstreamReader reader{sv};
  ASSERT_EQ(reader.ReadExponentialGolombCode(), 6);
  ASSERT_EQ(reader.ReadExponentialGolombCode(), 2);

  // Can't read anything else.
  ASSERT_THROW(reader.ReadExponentialGolombCode(), std::out_of_range);
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
