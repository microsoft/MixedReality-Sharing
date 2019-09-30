// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "src/VersionRefCount.h"

#include <vector>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

TEST(BlobDetails, VersionRefCount) {
  static_assert(sizeof(VersionRefCount) == sizeof(uint32_t));

  static constexpr uint32_t kVersionsCount = 10;
  uint32_t memory[kVersionsCount] = {};
  VersionRefCount::Accessor accessor{
      reinterpret_cast<VersionRefCount*>(&memory[kVersionsCount - 1])};

  for (uint32_t i = 0; i < kVersionsCount; ++i)
    accessor.InitVersion(VersionOffset{i});

  auto CheckVersions = [&](std::vector<uint32_t> expected_versions) {
    std::vector<uint32_t> actual;
    accessor.ForEachAliveVersion(kVersionsCount, [&](VersionOffset offset) {
      actual.emplace_back(static_cast<uint32_t>(offset));
      return false;
    });
    EXPECT_EQ(actual, expected_versions);
  };

  CheckVersions({0, 1, 2, 3, 4, 5, 6, 7, 8, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{8}));
  CheckVersions({0, 1, 2, 3, 4, 5, 6, 7, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{1}));
  CheckVersions({0, 2, 3, 4, 5, 6, 7, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{6}));
  CheckVersions({0, 2, 3, 4, 5, 7, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{7}));
  CheckVersions({0, 2, 3, 4, 5, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{2}));
  CheckVersions({0, 3, 4, 5, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{0}));
  CheckVersions({3, 4, 5, 9});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{9}));
  CheckVersions({3, 4, 5});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{5}));
  CheckVersions({3, 4});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{3}));
  CheckVersions({4});

  EXPECT_TRUE(accessor.RemoveReference(VersionOffset{4}));
  CheckVersions({});
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
