// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "src/SubkeyVersionBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

class SubkeyVersionBlock_Test : public ::testing::Test {
 protected:
  ~SubkeyVersionBlock_Test() override {
    // At least one block was created (even if we failed to finalize).
    EXPECT_GT(stored_data_blocks_count_with_offset_,
              kStoredDataBlocksCountOffset);
    // The limit of stored blocks wasn't exceeded, even in case of failure.
    EXPECT_LE(stored_data_blocks_count(), kAvailableBlocksCount);
  }

 protected:
  std::vector<VersionedPayloadHandle> GatherHandles() noexcept {
    std::vector<VersionedPayloadHandle> handles;
    EXPECT_EQ(first_block_.AppendPayloads(handles), DataBlockLocation{42});
    return handles;
  }

  static constexpr uint32_t kAvailableBlocksCount = 10;
  alignas(64) char memory_[kBlockSize * kAvailableBlocksCount];
  SubkeyVersionBlock& first_block_ =
      *reinterpret_cast<SubkeyVersionBlock*>(memory_);

  static constexpr uint32_t kStoredDataBlocksCountOffset = 100000;

  uint32_t stored_data_blocks_count() const noexcept {
    return stored_data_blocks_count_with_offset_ - kStoredDataBlocksCountOffset;
  }

  uint32_t stored_data_blocks_count_with_offset_ = kStoredDataBlocksCountOffset;

  SubkeyVersionBlock::Builder builder_{DataBlockLocation{42}, first_block_,
                                       kAvailableBlocksCount,
                                       stored_data_blocks_count_with_offset_};

  // Takes a few samples around the first_version, the last_version and some
  // versions in-between.
  bool IsMissingPayloadsBetween(uint64_t first_version,
                                uint64_t last_version) const noexcept {
    assert(first_version <= last_version &&
           last_version < kInvalidVersion);
    for (uint64_t i = 0; i < 10 && first_version + i < last_version; ++i) {
      if (first_block_.GetVersionedPayload(first_version + i).has_payload())
        return false;
    }
    for (uint64_t i = 0; i < 10 && last_version - i > first_version; ++i) {
      if (first_block_.GetVersionedPayload(last_version - i).has_payload())
        return false;
    }
    const uint64_t increment =
        std::max(1ull, (last_version - first_version + 1) / 1000);

    for (uint64_t i = first_version; i < last_version; i += increment) {
      if (first_block_.GetVersionedPayload(i).has_payload())
        return false;
    }
    return true;
  }

  bool IsPayloadBetween(uint64_t first_version,
                        uint64_t last_version,
                        uint64_t expected_handle_64) const noexcept {
    assert(first_version <= last_version &&
           last_version < kInvalidVersion);
    VersionedPayloadHandle expected_handle{first_version,
                                           PayloadHandle{expected_handle_64}};

    for (uint64_t i = 0; i < 10 && first_version + i < last_version; ++i) {
      if (first_block_.GetVersionedPayload(first_version + i) !=
          expected_handle)
        return false;
    }
    for (uint64_t i = 0; i < 10 && last_version - i > first_version; ++i) {
      if (first_block_.GetVersionedPayload(last_version - i) != expected_handle)
        return false;
    }
    const uint64_t increment =
        std::max(1ull, (last_version - first_version + 1) / 1000);

    for (uint64_t i = first_version; i < last_version; i += increment) {
      if (first_block_.GetVersionedPayload(i) != expected_handle)
        return false;
    }
    return true;
  }
};

TEST_F(SubkeyVersionBlock_Test, starting_from_empty) {
  // The builder starts with an empty state, so this push should have no effect.
  // This emulates the scenario where we are reallocating the version block for
  // a subkey that is currently missing.
  ASSERT_TRUE(builder_.Push(122'000'000'000ull, {}));
  ASSERT_TRUE(builder_.FinalizeAndReserveOne(123'000'000'000ull, true));
  EXPECT_EQ(stored_data_blocks_count(), 1);
  EXPECT_EQ(first_block_.size_relaxed(), 0);
  EXPECT_EQ(first_block_.capacity(), 4);

  EXPECT_EQ(first_block_.latest_versioned_payload_thread_unsafe(),
            VersionedPayloadHandle{});
  EXPECT_TRUE(IsMissingPayloadsBetween(0, 123'000'000'000ull));
  EXPECT_TRUE(GatherHandles().empty());

  // The only block has 4 empty slots (all blocks after the first one would have
  // 5 slots each, but we don't have them here).

  {
    // The first push should succeed even for a very large version,
    // since the first version of each block is not compressed and saved as is.
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(123'000'000'000ull, true));
    first_block_.PushFromWriterThread(123'000'000'000ull,
                                      PayloadHandle{123'000'000});

    EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));

    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'000ull, 125'000'000'000ull, 123'000'000));

    std::vector<VersionedPayloadHandle> expected_handles{
        {123'000'000'000ull, PayloadHandle{123'000'000}}};
    EXPECT_EQ(GatherHandles(), expected_handles);
  }

  // This version can be compressed into the same block because because the
  // difference between marked versions (which are versions with a bit
  // indicating a deletion marker) is small enough.
  ASSERT_TRUE(first_block_.CanPushFromWriterThread(125'147'483'647ull, true));

  // This one can't (it's the same version as above, but the deletion marker
  // makes the offset too large).
  EXPECT_FALSE(first_block_.CanPushFromWriterThread(125'147'483'647ull, false));

  {
    // Pushing the second version.
    EXPECT_EQ(first_block_.size_relaxed(), 1);
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(123'000'000'100ull, true));
    first_block_.PushFromWriterThread(123'000'000'100ull,
                                      PayloadHandle{123'000'100});
    EXPECT_EQ(first_block_.size_relaxed(), 2);
    // Still fits (it's compressible and there are empty slots).
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(125'147'483'647ull, true));

    EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'000ull, 123'000'000'099ull, 123'000'000));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'100ull, 123'000'000'199ull, 123'000'100));

    std::vector<VersionedPayloadHandle> expected_handles{
        {123'000'000'000ull, PayloadHandle{123'000'000}},
        {123'000'000'100ull, PayloadHandle{123'000'100}}};
    EXPECT_EQ(GatherHandles(), expected_handles);
  }

  {
    // Adding a deletion marker.
    EXPECT_EQ(first_block_.size_relaxed(), 2);
    ASSERT_TRUE(
        first_block_.CanPushFromWriterThread(123'000'000'200ull, false));
    first_block_.PushFromWriterThread(123'000'000'200ull, {});
    EXPECT_EQ(first_block_.size_relaxed(), 3);
    // Still fits (it's compressible and there are empty slots).
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(125'147'483'647ull, true));

    EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'000ull, 123'000'000'099ull, 123'000'000));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'100ull, 123'000'000'199ull, 123'000'100));
    EXPECT_TRUE(
        IsMissingPayloadsBetween(123'000'000'200ull, 125'000'000'200ull));

    // Same payloads as before: the deletion marker shouldn't be returned
    std::vector<VersionedPayloadHandle> expected_handles{
        {123'000'000'000ull, PayloadHandle{123'000'000}},
        {123'000'000'100ull, PayloadHandle{123'000'100}}};
    EXPECT_EQ(GatherHandles(), expected_handles);
  }

  {
    // Adding the last version.
    EXPECT_EQ(first_block_.size_relaxed(), 3);
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(123'000'000'300ull, true));
    first_block_.PushFromWriterThread(123'000'000'300ull,
                                      PayloadHandle{123'000'300});
    EXPECT_EQ(first_block_.size_relaxed(), 4);
    // The code above validated that this version is compressible, but now it
    // doesn't fit due to capacity limitations.
    EXPECT_FALSE(
        first_block_.CanPushFromWriterThread(125'147'483'647ull, true));
    // Even smaller versions won't fit.
    EXPECT_FALSE(
        first_block_.CanPushFromWriterThread(123'000'000'301ull, true));

    EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'000ull, 123'000'000'099ull, 123'000'000));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'100ull, 123'000'000'199ull, 123'000'100));
    EXPECT_TRUE(
        IsMissingPayloadsBetween(123'000'000'200ull, 123'000'000'299ull));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'300ull, 125'000'000'000ull, 123'000'300));

    std::vector<VersionedPayloadHandle> expected_handles{
        {123'000'000'000ull, PayloadHandle{123'000'000}},
        {123'000'000'100ull, PayloadHandle{123'000'100}},
        {123'000'000'300ull, PayloadHandle{123'000'300}}};
    EXPECT_EQ(GatherHandles(), expected_handles);
  }
}

TEST_F(SubkeyVersionBlock_Test, big_gaps) {
  // Simulating a rare situation when the gap between versions is so large
  // that the version can't be saved as an offset from the base version.
  // This can happen if some rarely modified subkey stayed unchanged for a very
  // long time, surviving multiple reallocations of the storage blob, and was
  // finally edited about 2 billions of versions later.
  ASSERT_TRUE(builder_.Push(
      123'000'000'000ull,
      VersionedPayloadHandle{123'000'000'000ull, PayloadHandle{123'000'000}}));
  // This push will immediately start the next block since the version can't be
  // compressed.
  ASSERT_TRUE(builder_.Push(125'147'483'647ull, {}));
  ASSERT_TRUE(builder_.FinalizeAndReserveOne(126'000'000'000ull, true));
  // Even though there are only 2 elements, 4 slots of the first block will be
  // filled with invalid version offsets. This bumps the size to 5 and forces a
  // larger reserve.
  EXPECT_EQ(stored_data_blocks_count(), 3);
  EXPECT_EQ(first_block_.size_relaxed(), 5);
  EXPECT_EQ(first_block_.capacity(), 14);
  EXPECT_EQ(first_block_.latest_versioned_payload_thread_unsafe(),
            VersionedPayloadHandle{});

  EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));
  EXPECT_TRUE(
      IsPayloadBetween(123'000'000'000ull, 125'147'483'646ull, 123'000'000));
  EXPECT_TRUE(IsMissingPayloadsBetween(125'147'483'647ull, 222'000'000'000ull));

  {
    // Adding one version to the second block (the version should be
    // compressible relative to the first version of the block).
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(126'000'000'000ull, true));
    first_block_.PushFromWriterThread(126'000'000'000ull, PayloadHandle{126});
    EXPECT_EQ(first_block_.size_relaxed(), 6);

    EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'000ull, 125'147'483'646ull, 123'000'000));
    EXPECT_TRUE(
        IsMissingPayloadsBetween(125'147'483'647ull, 125'999'999'999ull));
    EXPECT_TRUE(IsPayloadBetween(126'000'000'000ull, 127'000'000'000ull, 126));

    // The handles are gathered from both blocks
    std::vector<VersionedPayloadHandle> expected_handles{
        {123'000'000'000ull, PayloadHandle{123'000'000}},
        {126'000'000'000ull, PayloadHandle{126}}};
    EXPECT_EQ(GatherHandles(), expected_handles);
  }

  {
    // Adding an extra version that shouldn't fit into the second block
    // because it can't be compressed into an offset.
    // Like in a similar situation above (when the builder tried to do the
    // same),  the new version should be pushed into the new block, and the
    // wasted slots filled with invalid offsets.
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(127'294'967'295ull, true));
    first_block_.PushFromWriterThread(127'294'967'295ull, PayloadHandle{127});
    // This bumps the size to 10 (4 elements in the first block, 5 elements in
    // the second, 1 element in the third).
    EXPECT_EQ(first_block_.size_relaxed(), 10);

    // Adding the largest possible version that is still compressible as
    // an offset in the 3rd block.
    ASSERT_TRUE(first_block_.CanPushFromWriterThread(129'442'450'942ull, true));
    // Validating that the next one wouldn't fit (and it can't migrate to
    // the next block since the builder allocated only 3 blocks).
    EXPECT_FALSE(
        first_block_.CanPushFromWriterThread(129'442'450'943ull, true));

    first_block_.PushFromWriterThread(129'442'450'942ull, PayloadHandle{129});
    EXPECT_EQ(first_block_.size_relaxed(), 11);

    EXPECT_TRUE(IsMissingPayloadsBetween(0, 122'999'999'999ull));
    EXPECT_TRUE(
        IsPayloadBetween(123'000'000'000ull, 125'147'483'646ull, 123'000'000));
    EXPECT_TRUE(
        IsMissingPayloadsBetween(125'147'483'647ull, 125'999'999'999ull));
    EXPECT_TRUE(IsPayloadBetween(126'000'000'000ull, 127'294'967'294ull, 126));
    EXPECT_TRUE(IsPayloadBetween(127'294'967'295ull, 129'442'450'941ull, 127));
    EXPECT_TRUE(IsPayloadBetween(129'442'450'942ull, 200'000'000'000ull, 129));

    // The handles are gathered from all three blocks.
    std::vector<VersionedPayloadHandle> expected_handles{
        {123'000'000'000ull, PayloadHandle{123'000'000}},
        {126'000'000'000ull, PayloadHandle{126}},
        {127'294'967'295ull, PayloadHandle{127}},
        {129'442'450'942ull, PayloadHandle{129}}};
    EXPECT_EQ(GatherHandles(), expected_handles);
  }
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
