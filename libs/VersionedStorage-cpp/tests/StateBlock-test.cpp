// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "src/StateBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

TEST(StateBlock, KeyStateBlock) {
  // KeyStateBlock can hold up to 3 versions in-place.

  KeyStateBlock block{KeyHandle{42}, KeySubscriptionHandle{1234}};

  // Initially there are no versions, and the observed subkeys count is 0.
  // This is a normal case for a key block that was added just for subscriptions
  // (or to contain a subkey block that was added just for subscriptions).

  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(0)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset{~1u}), 0);
  EXPECT_TRUE(block.HasFreeInPlaceSlots());

  // All versions before the published one should observe the absence of
  // subkeys.
  block.PushSubkeysCount(VersionOffset{1000}, 2000);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(0)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(999)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1000)), 2000);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset{~1u}), 2000);
  EXPECT_TRUE(block.HasFreeInPlaceSlots());

  block.PushSubkeysCount(VersionOffset{1005}, 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(0)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(999)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1000)), 2000);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1004)), 2000);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1005)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset{~1u}), 0);
  EXPECT_TRUE(block.HasFreeInPlaceSlots());

  block.PushSubkeysCount(VersionOffset{1010}, 2010);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(0)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(999)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1000)), 2000);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1004)), 2000);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1005)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1009)), 0);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset(1010)), 2010);
  EXPECT_EQ(block.GetSubkeysCount(VersionOffset{~1u}), 2010);
  EXPECT_FALSE(block.HasFreeInPlaceSlots());

  // The writes above should not overwrite the other properties.
  EXPECT_EQ(block.key_, KeyHandle{42});
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), KeySubscriptionHandle{1234});
  EXPECT_EQ(block.tree_level(), 0);
  ASSERT_FALSE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.left_tree_child_, DataBlockLocation::kInvalid);
  EXPECT_EQ(block.right_tree_child_, DataBlockLocation::kInvalid);

  for (int i = 0; i < 10; ++i)
    block.IncrementTreeLevel();

  EXPECT_EQ(block.key_, KeyHandle{42});
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), KeySubscriptionHandle{1234});
  EXPECT_EQ(block.tree_level(), 10);
  ASSERT_FALSE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.left_tree_child_, DataBlockLocation::kInvalid);
  EXPECT_EQ(block.right_tree_child_, DataBlockLocation::kInvalid);

  block.SetScratchBuffer(reinterpret_cast<void*>(999'999'999));
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), KeySubscriptionHandle{1234});
  ASSERT_TRUE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.GetScratchBuffer(), reinterpret_cast<void*>(999'999'999));
}

TEST(StateBlock, SubkeyStateBlock_payload_and_deletion_marker) {
  // Publishing a payload and a deletion marker (with a version small enough to
  // be compressed into the same SubkeyStateBlock).

  SubkeyStateBlock block{KeyHandle{42}, SubkeySubscriptionHandle{1234},
                         3141592653589793238ull};

  EXPECT_FALSE(block.GetVersionedPayload(0).has_payload());
  EXPECT_FALSE(block.GetVersionedPayload(99999).has_payload());
  EXPECT_FALSE(
      block.GetVersionedPayload(kSmallestInvalidVersion - 1).has_payload());

  // Can store any version at first, since it's not compressed.
  EXPECT_TRUE(block.CanPush(0, true));
  EXPECT_TRUE(block.CanPush(0, false));
  EXPECT_TRUE(block.CanPush(123'000'000'000ull, true));
  EXPECT_TRUE(block.CanPush(123'000'000'000ull, false));
  EXPECT_TRUE(block.CanPush(223'000'000'000ull, true));
  EXPECT_TRUE(block.CanPush(223'000'000'000ull, false));

  block.Push(123'000'000'000ull, PayloadHandle{123'123'000});
  EXPECT_FALSE(block.GetVersionedPayload(0).has_payload());
  EXPECT_FALSE(block.GetVersionedPayload(122'999'999'999ull).has_payload());
  EXPECT_TRUE(block.GetVersionedPayload(123'000'000'000ull).has_payload());

  VersionedPayloadHandle v0{123'000'000'000ull, PayloadHandle{123'123'000}};

  EXPECT_EQ(block.GetVersionedPayload(123'000'000'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(123'000'400'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(223'000'000'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(kSmallestInvalidVersion - 1), v0);

  EXPECT_TRUE(block.CanPush(124'000'000'000ull, false));
  block.Push(124'000'000'000ull, {});

  EXPECT_FALSE(block.GetVersionedPayload(0).has_payload());
  EXPECT_FALSE(block.GetVersionedPayload(122'999'999'999ull).has_payload());
  EXPECT_TRUE(block.GetVersionedPayload(123'000'000'000ull).has_payload());
  EXPECT_EQ(block.GetVersionedPayload(123'000'000'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(123'000'400'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(123'999'999'999ull), v0);
  EXPECT_FALSE(block.GetVersionedPayload(124'000'000'000ull).has_payload());
  EXPECT_FALSE(
      block.GetVersionedPayload(kSmallestInvalidVersion - 1).has_payload());

  // The writes above should not overwrite the other properties.
  EXPECT_EQ(block.key_, KeyHandle{42});
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), SubkeySubscriptionHandle{1234});
  EXPECT_EQ(block.tree_level(), 0);
  ASSERT_FALSE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.left_tree_child_, DataBlockLocation::kInvalid);
  EXPECT_EQ(block.right_tree_child_, DataBlockLocation::kInvalid);

  for (int i = 0; i < 10; ++i)
    block.IncrementTreeLevel();

  EXPECT_EQ(block.key_, KeyHandle{42});
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), SubkeySubscriptionHandle{1234});
  EXPECT_EQ(block.tree_level(), 10);
  ASSERT_FALSE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.left_tree_child_, DataBlockLocation::kInvalid);
  EXPECT_EQ(block.right_tree_child_, DataBlockLocation::kInvalid);

  block.SetScratchBuffer(reinterpret_cast<void*>(999'999'999));
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), SubkeySubscriptionHandle{1234});
  ASSERT_TRUE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.GetScratchBuffer(), reinterpret_cast<void*>(999'999'999));
}

TEST(StateBlock, SubkeyStateBlock_2_payloads_largest_offset) {
  SubkeyStateBlock block{KeyHandle{42}, SubkeySubscriptionHandle{1234},
                         3141592653589793238ull};

  EXPECT_FALSE(block.GetVersionedPayload(0).has_payload());
  EXPECT_FALSE(block.GetVersionedPayload(99999).has_payload());
  EXPECT_FALSE(
      block.GetVersionedPayload(kSmallestInvalidVersion - 1).has_payload());

  // Can store any version at first, since it's not compressed.
  EXPECT_TRUE(block.CanPush(0, true));
  EXPECT_TRUE(block.CanPush(0, false));
  EXPECT_TRUE(block.CanPush(123'000'000'000ull, true));
  EXPECT_TRUE(block.CanPush(123'000'000'000ull, false));
  EXPECT_TRUE(block.CanPush(223'000'000'000ull, true));
  EXPECT_TRUE(block.CanPush(223'000'000'000ull, false));
  EXPECT_TRUE(block.CanPush(kSmallestInvalidVersion - 1, true));
  EXPECT_TRUE(block.CanPush(kSmallestInvalidVersion - 1, false));

  block.Push(123'000'000'000ull, PayloadHandle{123'123'000});
  EXPECT_FALSE(block.GetVersionedPayload(0).has_payload());
  EXPECT_FALSE(block.GetVersionedPayload(122'999'999'999ull).has_payload());
  EXPECT_TRUE(block.GetVersionedPayload(123'000'000'000ull).has_payload());

  VersionedPayloadHandle v0{123'000'000'000ull, PayloadHandle{123'123'000}};

  EXPECT_EQ(block.GetVersionedPayload(123'000'000'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(123'000'400'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(223'000'000'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(kSmallestInvalidVersion - 1), v0);

  EXPECT_TRUE(block.CanPush(124'000'000'000ull, true));
  EXPECT_TRUE(block.CanPush(124'000'000'000ull, false));

  // This version can be compressed into the same block because because the
  // difference between marked versions (which are versions with a bit
  // indicating a deletion marker) is small enough.
  EXPECT_TRUE(block.CanPush(125'147'483'647ull, true));

  // This one can't (it's the same version as above, but the deletion marker
  // makes the offset too large).
  EXPECT_FALSE(block.CanPush(125'147'483'647ull, false));

  block.Push(125'147'483'647ull, PayloadHandle{125'125'000});
  EXPECT_FALSE(block.GetVersionedPayload(0).has_payload());
  EXPECT_FALSE(block.GetVersionedPayload(122'999'999'999ull).has_payload());
  EXPECT_TRUE(block.GetVersionedPayload(123'000'000'000ull).has_payload());
  EXPECT_EQ(block.GetVersionedPayload(123'000'000'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(123'000'400'000ull), v0);
  EXPECT_EQ(block.GetVersionedPayload(125'147'483'646ull), v0);

  VersionedPayloadHandle v1{125'147'483'647ull, PayloadHandle{125'125'000}};
  EXPECT_EQ(block.GetVersionedPayload(125'147'483'647ull), v1);
  EXPECT_EQ(block.GetVersionedPayload(kSmallestInvalidVersion - 1), v1);

  // The writes above should not overwrite the other properties.
  EXPECT_EQ(block.key_, KeyHandle{42});
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), SubkeySubscriptionHandle{1234});
  EXPECT_EQ(block.tree_level(), 0);
  ASSERT_FALSE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.left_tree_child_, DataBlockLocation::kInvalid);
  EXPECT_EQ(block.right_tree_child_, DataBlockLocation::kInvalid);

  for (int i = 0; i < 10; ++i)
    block.IncrementTreeLevel();

  EXPECT_EQ(block.key_, KeyHandle{42});
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), SubkeySubscriptionHandle{1234});
  EXPECT_EQ(block.tree_level(), 10);
  ASSERT_FALSE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.left_tree_child_, DataBlockLocation::kInvalid);
  EXPECT_EQ(block.right_tree_child_, DataBlockLocation::kInvalid);

  block.SetScratchBuffer(reinterpret_cast<void*>(999'999'999));
  ASSERT_TRUE(block.has_subscription());
  EXPECT_EQ(block.subscription(), SubkeySubscriptionHandle{1234});
  ASSERT_TRUE(block.is_scratch_buffer_mode());
  EXPECT_EQ(block.GetScratchBuffer(), reinterpret_cast<void*>(999'999'999));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
