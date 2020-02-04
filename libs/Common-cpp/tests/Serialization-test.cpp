// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BlobReader.h>
#include <Microsoft/MixedReality/Sharing/Common/Serialization/BlobWriter.h>

namespace Microsoft::MixedReality::Sharing::Serialization {
using namespace std::literals;

namespace {
struct EncodedValue {
  uint64_t value;
  bit_shift_t width_bits;
};

// Generates a test set of values of different bit "widths" (the size of the
// significant part up to the highest non-zero bit). Compared to uniform
// distribution, values of smaller widths are a lot more likely (and each width
// is about as likely as any other). A special value ~0ull is also inserted with
// increased probability since it is special-cased in both the encoder and the
// decoder.
std::vector<EncodedValue> GenerateRandomValues(size_t count) {
  static const auto distributions = [] {
    std::vector<std::uniform_int_distribution<uint64_t>> result;
    for (uint64_t width = 1; width <= 64; ++width) {
      uint64_t max_value = ~0ull >> (64 - width);
      uint64_t min_value = (max_value / 2) + 1;
      result.emplace_back(min_value, max_value);
    }
    return result;
  }();
  static const std::uniform_int_distribution<bit_shift_t>
      generator_distribution{0, 64};
  static std::mt19937 rng{std::random_device{}()};

  std::vector<EncodedValue> values(count);
  for (auto& pair : values) {
    auto w = generator_distribution(rng);
    if (w == 64) {
      // Special-casing ~0ull to make it appear more frequently
      // (it has a special representation in our Golomb encoding)
      pair.value = ~0ull;
      pair.width_bits = 64;
    } else {
      pair.value = distributions[w](rng);
      pair.width_bits = w + 1;
    }
  }
  return values;
}
}  // namespace

TEST(Serialization, blob_write_read_bits_32) {
  // Writes and reads fixed sized patterns of ones and zeros.
  static constexpr size_t kRepeatsCount = 100;
  for (bit_shift_t zeros_count = 1; zeros_count <= 32; ++zeros_count) {
    for (bit_shift_t ones_count = 1; ones_count <= 32; ++ones_count) {
      BlobWriter writer;
      uint32_t value = ~0u >> (32 - ones_count);
      for (int i = 0; i < kRepeatsCount; ++i) {
        writer.WriteBits(0, zeros_count);
        writer.WriteBits(value, ones_count);
      }
      BlobReader reader{writer.Finalize()};
      for (int i = 0; i < kRepeatsCount; ++i) {
        ASSERT_EQ(reader.ReadBits32(zeros_count), 0);
        ASSERT_EQ(reader.ReadBits32(ones_count), value);
      }
      ASSERT_TRUE(reader.ProbablyNoMoreData());
    }
  }
}

TEST(Serialization, blob_write_read_bits_64) {
  // Writes and reads fixed sized patterns of ones and zeros.
  static constexpr size_t kRepeatsCount = 20;
  for (bit_shift_t zeros_count = 1; zeros_count <= 64; ++zeros_count) {
    for (bit_shift_t ones_count = 1; ones_count <= 64; ++ones_count) {
      BlobWriter writer;
      uint64_t value = ~0ull >> (64 - ones_count);
      for (int i = 0; i < kRepeatsCount; ++i) {
        writer.WriteBits(0, zeros_count);
        writer.WriteBits(value, ones_count);
      }
      BlobReader reader{writer.Finalize()};
      for (int i = 0; i < kRepeatsCount; ++i) {
        ASSERT_EQ(reader.ReadBits64(zeros_count), 0);
        ASSERT_EQ(reader.ReadBits64(ones_count), value);
      }
      ASSERT_TRUE(reader.ProbablyNoMoreData());
    }
  }
}

TEST(Serialization, blob_write_read_bits_random) {
  // Writes and reads random values of various sizes.
  auto values = GenerateRandomValues(20000);
  BlobWriter writer;
  for (const auto& x : values) {
    writer.WriteBits(x.value, x.width_bits);
  }
  BlobReader reader{writer.Finalize()};
  for (const auto& expected : values) {
    uint64_t read_value = reader.ReadBits64(expected.width_bits);
    ASSERT_EQ(expected.value, read_value);
  }
  ASSERT_TRUE(reader.ProbablyNoMoreData());
}

TEST(Serialization, blob_write_read_golomb_codes_short_sequences) {
  // Writing and reading short sequences of exponential-Golomb codes,
  // written from various offsets.
  static constexpr size_t kRerunsCount = 10;
  for (size_t i = 0; i < kRerunsCount; ++i) {
    for (size_t sequence_length = 1; sequence_length < 10; ++sequence_length) {
      for (bit_shift_t offset = 1; offset <= 64; ++offset) {
        auto values = GenerateRandomValues(sequence_length);
        BlobWriter writer;
        writer.WriteBits(0, offset);
        for (const auto& x : values) {
          writer.WriteGolomb(x.value);
        }
        BlobReader reader{writer.Finalize()};
        ASSERT_EQ(reader.ReadBits64(offset), 0);
        for (const auto& expected : values) {
          uint64_t read_value = reader.ReadGolomb();
          ASSERT_EQ(expected.value, read_value);
        }
        ASSERT_TRUE(reader.ProbablyNoMoreData());
      }
    }
  }
}

TEST(Serialization, blob_write_read_golomb_codes_long_sequence) {
  // Writes and reads multiple exponential-Golomb codes to the stream.
  auto values = GenerateRandomValues(20000);
  BlobWriter writer;
  for (const auto& x : values) {
    writer.WriteGolomb(x.value);
  }
  BlobReader reader{writer.Finalize()};
  for (const auto& expected : values) {
    uint64_t read_value = reader.ReadGolomb();
    ASSERT_EQ(expected.value, read_value);
  }
  ASSERT_TRUE(reader.ProbablyNoMoreData());
}

TEST(Serialization, blob_read_from_empty) {
  for (bit_shift_t width = 1; width <= 32; ++width) {
    BlobReader reader{{}};
    ASSERT_THROW(reader.ReadBits32(width), std::out_of_range);
  }

  for (bit_shift_t width = 1; width <= 64; ++width) {
    BlobReader reader{{}};
    ASSERT_THROW(reader.ReadBits64(width), std::out_of_range);
  }

  BlobReader reader{{}};
  ASSERT_THROW(reader.ReadGolomb(), std::out_of_range);
}

TEST(Serialization, blob_read_out_of_range) {
  BlobWriter writer;
  writer.WriteBits(0xFF, 8);
  auto sv = writer.Finalize();
  ASSERT_EQ(sv, "\xff"sv);
  BlobReader reader{sv};
  ASSERT_EQ(reader.ReadBits32(4), 0xF);
  ASSERT_EQ(reader.ReadBits32(4), 0xF);

  // Can't read anything else
  ASSERT_THROW(reader.ReadBits32(1), std::out_of_range);
}

TEST(Serialization, blob_read_golomb_out_of_range) {
  BlobWriter writer;
  writer.WriteGolomb(6);
  writer.WriteGolomb(2);
  auto sv = writer.Finalize();

  // The order is: low bits => high bits.
  // 110 | 11100
  // ||    ||| ^ High bit
  // ||    ||^ Separator bit, the number of leading zeros is 2
  // ||    ^^ After reading 111b, we subtract 1 to get 6.
  // |^ Separator of the next number. 1 leading zero before it.
  // ^ After reading 11b we subtract 1 to get 2.

  ASSERT_EQ(sv, "\x3B"sv);
  BlobReader reader{sv};
  ASSERT_EQ(reader.ReadGolomb(), 6);
  ASSERT_EQ(reader.ReadGolomb(), 2);

  // Can't read anything else.
  ASSERT_THROW(reader.ReadGolomb(), std::out_of_range);
}

}  // namespace Microsoft::MixedReality::Sharing::Serialization
