// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "src/KeyVersionBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

class KeyVersionBlock_Test : public ::testing::Test {
 protected:
  ~KeyVersionBlock_Test() override {
    // At least one block was created (even if we failed to finalize).
    EXPECT_GT(stored_data_blocks_count_with_offset_,
              kStoredDataBlocksCountOffset);
    // The limit of stored blocks wasn't exceeded, even in case of failure.
    EXPECT_LE(stored_data_blocks_count(), kAvailableBlocksCount);
  }

 protected:
  static constexpr uint32_t kAvailableBlocksCount = 10;
  alignas(64) char memory_[kBlockSize * kAvailableBlocksCount];
  KeyVersionBlock& first_block_ = *reinterpret_cast<KeyVersionBlock*>(memory_);

  static constexpr uint32_t kStoredDataBlocksCountOffset = 100000;

  uint32_t stored_data_blocks_count() const noexcept {
    return stored_data_blocks_count_with_offset_ - kStoredDataBlocksCountOffset;
  }

  uint32_t stored_data_blocks_count_with_offset_ = kStoredDataBlocksCountOffset;

  KeyVersionBlock::Builder builder_{first_block_, kAvailableBlocksCount,
                                    stored_data_blocks_count_with_offset_};
};

TEST_F(KeyVersionBlock_Test, empty) {
  EXPECT_TRUE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 1);
  EXPECT_EQ(first_block_.size_relaxed(), 0);
  EXPECT_EQ(first_block_.capacity(), 7);

  for (uint32_t i = 0; i < 10; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 0);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 0);

  // Has 7 empty slots after the builder is done.

  for (uint32_t i = 0; i < 7; ++i) {
    EXPECT_TRUE(first_block_.has_empty_slots_thread_unsafe());
    first_block_.PushSubkeysCountFromWriterThread(VersionOffset{10 + i},
                                                  100 + i);
  }
  EXPECT_FALSE(first_block_.has_empty_slots_thread_unsafe());

  for (uint32_t i = 0; i < 7; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{10 + i}), 100 + i);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 106);
}

TEST_F(KeyVersionBlock_Test, empty_pushing_zeros) {
  for (uint32_t i = 0; i < 10; ++i)
    EXPECT_TRUE(builder_.Push(VersionOffset{i}, 0));

  EXPECT_TRUE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 1);
  EXPECT_EQ(first_block_.size_relaxed(), 0);
  EXPECT_EQ(first_block_.capacity(), 7);

  for (uint32_t i = 0; i < 20; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 0);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 0);

  // Has 7 empty slots after the builder is done.

  for (uint32_t i = 0; i < 7; ++i) {
    EXPECT_TRUE(first_block_.has_empty_slots_thread_unsafe());
    first_block_.PushSubkeysCountFromWriterThread(VersionOffset{10 + i},
                                                  100 + i);
  }
  EXPECT_FALSE(first_block_.has_empty_slots_thread_unsafe());

  for (uint32_t i = 0; i < 7; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{10 + i}), 100 + i);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 106);
}

TEST_F(KeyVersionBlock_Test, pushing_3_normal) {
  EXPECT_TRUE(builder_.Push(VersionOffset{10}, 101));
  EXPECT_TRUE(builder_.Push(VersionOffset{15}, 101));  // no effect

  EXPECT_TRUE(builder_.Push(VersionOffset{20}, 102));
  EXPECT_TRUE(builder_.Push(VersionOffset{25}, 102));  // no effect

  EXPECT_TRUE(builder_.Push(VersionOffset{30}, 103));
  EXPECT_TRUE(builder_.Push(VersionOffset{35}, 103));  // no effect

  EXPECT_TRUE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 1);
  EXPECT_EQ(first_block_.size_relaxed(), 3);
  EXPECT_EQ(first_block_.capacity(), 7);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 103);

  // Has 4 empty slots after the builder is done.

  for (uint32_t i = 0; i < 4; ++i) {
    EXPECT_TRUE(first_block_.has_empty_slots_thread_unsafe());
    first_block_.PushSubkeysCountFromWriterThread(VersionOffset{100 + i},
                                                  200 + i);
  }
  EXPECT_FALSE(first_block_.has_empty_slots_thread_unsafe());

  for (uint32_t i = 0; i < 10; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 0);

  for (uint32_t i = 10; i < 20; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 101);

  for (uint32_t i = 20; i < 30; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 102);

  for (uint32_t i = 30; i < 100; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 103);

  for (uint32_t i = 0; i < 4; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{100 + i}), 200 + i);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 203);
}

TEST_F(KeyVersionBlock_Test, pushing_4_normal) {
  EXPECT_TRUE(builder_.Push(VersionOffset{10}, 101));
  EXPECT_TRUE(builder_.Push(VersionOffset{15}, 101));  // no effect

  EXPECT_TRUE(builder_.Push(VersionOffset{20}, 102));
  EXPECT_TRUE(builder_.Push(VersionOffset{25}, 102));  // no effect

  EXPECT_TRUE(builder_.Push(VersionOffset{30}, 103));
  EXPECT_TRUE(builder_.Push(VersionOffset{35}, 103));  // no effect

  EXPECT_TRUE(builder_.Push(VersionOffset{40}, 104));
  EXPECT_TRUE(builder_.Push(VersionOffset{45}, 104));  // no effect

  EXPECT_TRUE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 2);
  EXPECT_EQ(first_block_.size_relaxed(), 4);
  EXPECT_EQ(first_block_.capacity(), 15);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 104);

  // Has 11 empty slots after the builder is done.

  for (uint32_t i = 0; i < 11; ++i) {
    EXPECT_TRUE(first_block_.has_empty_slots_thread_unsafe());
    first_block_.PushSubkeysCountFromWriterThread(VersionOffset{100 + i},
                                                  200 + i);
  }
  EXPECT_FALSE(first_block_.has_empty_slots_thread_unsafe());

  for (uint32_t i = 0; i < 10; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 0);

  for (uint32_t i = 10; i < 20; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 101);

  for (uint32_t i = 20; i < 30; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 102);

  for (uint32_t i = 30; i < 40; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 103);

  for (uint32_t i = 40; i < 100; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 104);

  for (uint32_t i = 0; i < 11; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{100 + i}), 200 + i);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 210);
}

TEST_F(KeyVersionBlock_Test, pushing_36_no_gaps) {
  // Should successfully reserve 79 slots.
  for (uint32_t i = 10; i < 46; ++i)
    EXPECT_TRUE(builder_.Push(VersionOffset{i}, 100 - i));
  EXPECT_TRUE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 10);
  EXPECT_EQ(first_block_.size_relaxed(), 36);
  EXPECT_EQ(first_block_.capacity(), 79);

  // Has 43 empty slots after the builder is done.

  for (uint32_t i = 0; i < 43; ++i) {
    EXPECT_TRUE(first_block_.has_empty_slots_thread_unsafe());
    first_block_.PushSubkeysCountFromWriterThread(VersionOffset{100 + i},
                                                  200 + i);
  }
  EXPECT_FALSE(first_block_.has_empty_slots_thread_unsafe());

  for (uint32_t i = 0; i < 10; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 0);

  for (uint32_t i = 10; i < 46; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 100 - i);

  for (uint32_t i = 46; i < 100; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 55);

  for (uint32_t i = 0; i < 43; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{100 + i}), 200 + i);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 242);
}

TEST_F(KeyVersionBlock_Test, pushing_78_no_gaps) {
  // This won't reserve the optimal number of slots,
  // but will still succeed by reserving just one extra.
  for (uint32_t i = 10; i < 88; ++i)
    EXPECT_TRUE(builder_.Push(VersionOffset{i}, 100 - i));
  EXPECT_TRUE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 10);
  EXPECT_EQ(first_block_.size_relaxed(), 78);
  EXPECT_EQ(first_block_.capacity(), 79);

  EXPECT_TRUE(first_block_.has_empty_slots_thread_unsafe());
  first_block_.PushSubkeysCountFromWriterThread(VersionOffset{100}, 200);
  EXPECT_FALSE(first_block_.has_empty_slots_thread_unsafe());

  for (uint32_t i = 0; i < 10; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 0);

  for (uint32_t i = 10; i < 88; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 100 - i);

  for (uint32_t i = 88; i < 100; ++i)
    EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{i}), 13);

  EXPECT_EQ(first_block_.GetSubkeysCount(VersionOffset{100}), 200);

  EXPECT_EQ(first_block_.latest_subkeys_count_thread_unsafe(), 200);
}

TEST_F(KeyVersionBlock_Test, pushing_79_fail_to_finalize) {
  for (uint32_t i = 10; i < 89; ++i)
    EXPECT_TRUE(builder_.Push(VersionOffset{i}, 100 - i));

  EXPECT_FALSE(builder_.FinalizeAndReserveOne());
  EXPECT_EQ(stored_data_blocks_count(), 10);
}

TEST_F(KeyVersionBlock_Test, pushing_80_fail_to_push) {
  for (uint32_t i = 10; i < 89; ++i)
    EXPECT_TRUE(builder_.Push(VersionOffset{i}, 100 - i));

  EXPECT_FALSE(builder_.Push(VersionOffset{89}, 21));
  EXPECT_EQ(stored_data_blocks_count(), 10);
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
