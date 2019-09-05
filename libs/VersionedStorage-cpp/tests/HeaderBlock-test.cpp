// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "src/HeaderBlock.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptorWithHandle.h>

#include "TestBehavior.h"
#include "src/StateBlock.h"

#include <memory>
#include <random>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class HeaderBlock_Test : public ::testing::Test {
 protected:
  ~HeaderBlock_Test() override {
    behavior_->CheckLeakingHandles();
    EXPECT_EQ(behavior_.use_count(), 1);
  }

 protected:
  static constexpr uint64_t kBaseVersion = 111'222'333'444'555ull;

  std::shared_ptr<TestBehavior> behavior_{std::make_shared<TestBehavior>()};

  KeyDescriptorWithHandle MakeKeyDescriptor(uint64_t id) const noexcept {
    return {*behavior_, behavior_->MakeKey(id), true};
  }

  PayloadHandle MakePayload(uint64_t id) const noexcept {
    return behavior_->MakePayload(id);
  }

  static bool HasLeftChild(const KeyBlockStateSearchResult& result) noexcept {
    assert(!result.state_block_->is_scratch_buffer_mode());
    return result.state_block_->left_tree_child_ != DataBlockLocation::kInvalid;
  }

  static bool HasRightChild(const KeyBlockStateSearchResult& result) noexcept {
    assert(!result.state_block_->is_scratch_buffer_mode());
    return result.state_block_->right_tree_child_ !=
           DataBlockLocation::kInvalid;
  }
  static bool IsLeaf(const KeyBlockStateSearchResult& result) noexcept {
    assert(!result.state_block_->is_scratch_buffer_mode());
    return result.state_block_->left_tree_child_ ==
               DataBlockLocation::kInvalid &&
           result.state_block_->right_tree_child_ ==
               DataBlockLocation::kInvalid;
  }
};

TEST_F(HeaderBlock_Test, CreateBlob_0_min_index_capacity) {
  HeaderBlock* header_block = HeaderBlock::CreateBlob(*behavior_, 12345, 0);

  ASSERT_NE(header_block, nullptr);
  EXPECT_EQ(header_block->base_version(), 12345);
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  MutatingBlobAccessor accessor(*header_block);

  // The header_block is always created together with its base version; the
  // caller is responsible for populating it with the correct state before
  // presenting it to other threads.
  EXPECT_EQ(header_block->stored_versions_count(), 1);
  EXPECT_EQ(accessor.keys_count(), 0);
  EXPECT_EQ(accessor.subkeys_count(), 0);
  // One index block (mask is 1 less).
  EXPECT_EQ(header_block->index_blocks_mask(), 0);
  // When there is only one index slot, it's allowed to be overpopulated.
  EXPECT_EQ(accessor.remaining_index_slots_capacity(), 7);

  // There are 64 blocks total; 1 is occupied by the header, and 1 is occupied
  // by the index. Note that the trailing data block is already used by the
  // base version's reference count.
  EXPECT_EQ(header_block->data_blocks_capacity(), 62);

  EXPECT_EQ(accessor.available_data_blocks_count(), 61);

  EXPECT_TRUE(accessor.CanInsertStateBlocks(7));
  EXPECT_FALSE(accessor.CanInsertStateBlocks(8));

  header_block->RemoveSnapshotReference(12345, *behavior_);
}

TEST_F(HeaderBlock_Test, CreateBlob_8_min_index_capacity) {
  HeaderBlock* header_block = HeaderBlock::CreateBlob(*behavior_, 12345, 8);
  ASSERT_NE(header_block, nullptr);
  EXPECT_EQ(header_block->base_version(), 12345);
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  MutatingBlobAccessor accessor(*header_block);

  // The header_block is always created together with its base version; the
  // caller is responsible for populating it with the correct state before
  // presenting it to other threads.
  EXPECT_EQ(header_block->stored_versions_count(), 1);
  EXPECT_EQ(accessor.keys_count(), 0);
  EXPECT_EQ(accessor.subkeys_count(), 0);
  // Two index blocks (mask is 1 less).
  EXPECT_EQ(header_block->index_blocks_mask(), 1);
  // Since there is more than one index block, we only allow up to 4 slots per
  // block.
  EXPECT_EQ(accessor.remaining_index_slots_capacity(), 8);

  // There are 64 blocks total; 1 is occupied by the header, and 2 are occupied
  // by the index. Note that the trailing data block is already used by the
  // base version's reference count.
  EXPECT_EQ(header_block->data_blocks_capacity(), 61);

  EXPECT_EQ(accessor.available_data_blocks_count(), 60);

  EXPECT_TRUE(accessor.CanInsertStateBlocks(8));
  EXPECT_FALSE(accessor.CanInsertStateBlocks(9));

  header_block->RemoveSnapshotReference(12345, *behavior_);
}

TEST_F(HeaderBlock_Test, CreateBlob_32_min_index_capacity) {
  HeaderBlock* header_block = HeaderBlock::CreateBlob(*behavior_, 12345, 32);
  ASSERT_NE(header_block, nullptr);
  EXPECT_EQ(header_block->base_version(), 12345);
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 2);

  MutatingBlobAccessor accessor(*header_block);

  // The header_block is always created together with its base version; the
  // caller is responsible for populating it with the correct state before
  // presenting it to other threads.
  EXPECT_EQ(header_block->stored_versions_count(), 1);
  EXPECT_EQ(accessor.keys_count(), 0);
  EXPECT_EQ(accessor.subkeys_count(), 0);
  // 8 index blocks (mask is 1 less).
  EXPECT_EQ(header_block->index_blocks_mask(), 7);
  // Since there is more than one index block, we only allow up to 4 slots per
  // block.
  EXPECT_EQ(accessor.remaining_index_slots_capacity(), 32);

  // There are 128 blocks total; 1 is occupied by the header, and 8 are occupied
  // by the index. Note that the trailing data block is already used by the
  // base version's reference count.
  EXPECT_EQ(header_block->data_blocks_capacity(), 119);

  EXPECT_EQ(accessor.available_data_blocks_count(), 118);

  EXPECT_TRUE(accessor.CanInsertStateBlocks(32));
  EXPECT_FALSE(accessor.CanInsertStateBlocks(33));

  header_block->RemoveSnapshotReference(12345, *behavior_);
}
TEST_F(HeaderBlock_Test, populate_block_index_with_keys) {
  // Populating the blob with various keys and checking that the internal data
  // structures behave adequately.
  // Each key ends up in:
  // * Hash index.
  // * Sorted list of keys.
  // * Tree of keys (used for fast insertion, not exposed to readers).
  // The trickiest part is the tree, and here we check all the relevant code
  // paths of the insertion procedure.

  HeaderBlock* header_block =
      HeaderBlock::CreateBlob(*behavior_, kBaseVersion, 7);
  ASSERT_NE(header_block, nullptr);
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  MutatingBlobAccessor accessor(*header_block);

  EXPECT_EQ(accessor.remaining_index_slots_capacity(), 7);
  EXPECT_TRUE(accessor.CanInsertStateBlocks(7));
  EXPECT_FALSE(accessor.CanInsertStateBlocks(8));

  // Returns KeyHandle{~0ull} if the child is missing, which would normally be a
  // valid handle, but here it's used as a marker.
  auto GetLeftChildKey = [&](const KeyBlockStateSearchResult& result) noexcept {
    if (!HasLeftChild(result))
      return KeyHandle{~0ull};
    DataBlockLocation left = result.state_block_->left_tree_child_;
    return accessor.GetBlockAt<KeyStateBlock>(left).key_;
  };
  auto GetRightChildKey = [&](
      const KeyBlockStateSearchResult& result) noexcept {
    if (!HasRightChild(result))
      return KeyHandle{~0ull};
    DataBlockLocation right = result.state_block_->right_tree_child_;
    return accessor.GetBlockAt<KeyStateBlock>(right).key_;
  };

  EXPECT_EQ(accessor.FindKey(MakeKeyDescriptor(20)).index_slot_location_,
            IndexSlotLocation::kInvalid);
  EXPECT_EQ(accessor.FindKey(MakeKeyDescriptor(20)).state_block_, nullptr);
  EXPECT_EQ(accessor.FindKey(MakeKeyDescriptor(20)).version_block_, nullptr);

  // Inserting key 20. This is the simplest case, since there is no other keys,
  // and thus the key will be inserted as a head of the list and the root of the
  // tree (of keys).
  accessor.InsertKeyBlock(MakeKeyDescriptor(20));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{20}), 1);
  KeyBlockStateSearchResult result_20 = accessor.FindKey(MakeKeyDescriptor(20));
  ASSERT_NE(result_20.state_block_, nullptr);
  EXPECT_EQ(result_20.state_block_->key_, KeyHandle{20});
  EXPECT_TRUE(IsLeaf(result_20));

  auto NextIs = [](KeyStateBlockEnumerator& e,
                   const KeyBlockStateSearchResult& r) -> bool {
    if (!e.MoveNext())
      return false;
    // This check is specific to this test.
    // All keys here should have no version blocks.
    if (e.CurrentVersionBlock())
      return false;
    return &e.CurrentStateBlock() == r.state_block_;
  };

  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Inserting key 10. It's less than the previous one, so it should be inserted
  // as a left child of the key 20. Then the AA-tree invariant should become
  // broken, and the skew operation will be performed, repairing the tree.
  // The new node (10) will become the new root.
  accessor.InsertKeyBlock(MakeKeyDescriptor(10));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{10}), 1);
  KeyBlockStateSearchResult result_10 = accessor.FindKey(MakeKeyDescriptor(10));
  ASSERT_NE(result_10.state_block_, nullptr);
  EXPECT_EQ(result_10.state_block_->key_, KeyHandle{10});

  // The tree looks like this (the number in () is the level of the node):
  // 10(0)    |
  //   \      |
  //    20(0) |
  EXPECT_EQ(result_10.state_block_->tree_level(), 0);
  EXPECT_EQ(result_20.state_block_->tree_level(), 0);

  EXPECT_FALSE(HasLeftChild(result_10));
  EXPECT_EQ(GetRightChildKey(result_10), KeyHandle{20});
  EXPECT_TRUE(IsLeaf(result_20));

  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_10));
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Inserting key 5. It's less than the root, and thus should be
  // inserted as the left child. It will break the invariant, but since
  // the root has the right child, the invariant can't be repaired by
  // skewing the tree. Instead, the level of the root will be
  // incremented.
  accessor.InsertKeyBlock(MakeKeyDescriptor(5));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{5}), 1);
  KeyBlockStateSearchResult result_5 = accessor.FindKey(MakeKeyDescriptor(5));
  ASSERT_NE(result_5.state_block_, nullptr);
  EXPECT_EQ(result_5.state_block_->key_, KeyHandle{5});

  // The tree looks like this (the number in () is the level of the node):
  //    10(1)    |
  //   /   \     |
  // 5(0)  20(0) |
  EXPECT_EQ(result_5.state_block_->tree_level(), 0);
  EXPECT_EQ(result_10.state_block_->tree_level(), 1);
  EXPECT_EQ(result_20.state_block_->tree_level(), 0);
  EXPECT_EQ(GetLeftChildKey(result_10), KeyHandle{5});
  EXPECT_EQ(GetRightChildKey(result_10), KeyHandle{20});
  EXPECT_TRUE(IsLeaf(result_5));
  EXPECT_TRUE(IsLeaf(result_20));

  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_5));
      EXPECT_TRUE(NextIs(enumerator, result_10));
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Inserting key 4. It's less than the root, and the left child is
  // already present. Therefore the insertion should recurse into adding
  // it as a child to 5. But then the invariant will become broken, and
  // the subtree will become skewed.
  accessor.InsertKeyBlock(MakeKeyDescriptor(4));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{4}), 1);
  KeyBlockStateSearchResult result_4 = accessor.FindKey(MakeKeyDescriptor(4));
  ASSERT_NE(result_4.state_block_, nullptr);
  EXPECT_EQ(result_4.state_block_->key_, KeyHandle{4});

  // The tree looks like this (the number in () is the level of the node):
  //    10(1)    |
  //   /   \     |
  // 4(0)  20(0) |
  //   \         |
  //   5(0)      |
  EXPECT_EQ(result_4.state_block_->tree_level(), 0);
  EXPECT_EQ(result_5.state_block_->tree_level(), 0);
  EXPECT_EQ(result_10.state_block_->tree_level(), 1);
  EXPECT_EQ(result_20.state_block_->tree_level(), 0);
  EXPECT_EQ(GetLeftChildKey(result_10), KeyHandle{4});
  EXPECT_EQ(GetRightChildKey(result_10), KeyHandle{20});
  EXPECT_FALSE(HasLeftChild(result_4));
  EXPECT_EQ(GetRightChildKey(result_4), KeyHandle{5});
  EXPECT_TRUE(IsLeaf(result_5));
  EXPECT_TRUE(IsLeaf(result_20));
  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_4));
      EXPECT_TRUE(NextIs(enumerator, result_5));
      EXPECT_TRUE(NextIs(enumerator, result_10));
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Inserting key 3. It should break the invariant twice. The first
  // time it will be repaired by incrementing the level, the second time
  // the skew operation will be performed. This test should validate
  // that children are properly preserved during the skew operation.
  accessor.InsertKeyBlock(MakeKeyDescriptor(3));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{3}), 1);
  KeyBlockStateSearchResult result_3 = accessor.FindKey(MakeKeyDescriptor(3));
  ASSERT_NE(result_3.state_block_, nullptr);
  EXPECT_EQ(result_3.state_block_->key_, KeyHandle{3});

  // The tree looks like this (the number in () is the level of the node):
  //    4(1)         |
  //   /   \         |
  // 3(0)  10(1)     |
  //       /  \      |
  //     5(0)  20(0) |
  EXPECT_EQ(result_3.state_block_->tree_level(), 0);
  EXPECT_EQ(result_4.state_block_->tree_level(), 1);
  EXPECT_EQ(result_5.state_block_->tree_level(), 0);
  EXPECT_EQ(result_10.state_block_->tree_level(), 1);
  EXPECT_EQ(result_20.state_block_->tree_level(), 0);

  EXPECT_EQ(GetLeftChildKey(result_10), KeyHandle{5});
  EXPECT_EQ(GetRightChildKey(result_10), KeyHandle{20});

  EXPECT_EQ(GetLeftChildKey(result_4), KeyHandle{3});
  EXPECT_EQ(GetRightChildKey(result_4), KeyHandle{10});

  EXPECT_TRUE(IsLeaf(result_3));
  EXPECT_TRUE(IsLeaf(result_5));
  EXPECT_TRUE(IsLeaf(result_20));

  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_3));
      EXPECT_TRUE(NextIs(enumerator, result_4));
      EXPECT_TRUE(NextIs(enumerator, result_5));
      EXPECT_TRUE(NextIs(enumerator, result_10));
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Inserting key 15. It should recurse into the right subtree and be inserted
  // with one skew.
  accessor.InsertKeyBlock(MakeKeyDescriptor(15));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{15}), 1);
  KeyBlockStateSearchResult result_15 = accessor.FindKey(MakeKeyDescriptor(15));
  ASSERT_NE(result_15.state_block_, nullptr);
  EXPECT_EQ(result_15.state_block_->key_, KeyHandle{15});

  // The tree looks like this (the number in () is the level of the node):
  //    4(1)          |
  //   /   \          |
  // 3(0)  10(1)      |
  //       /  \       |
  //     5(0)  15(0)  |
  //            \     |
  //            20(0) |
  EXPECT_EQ(result_3.state_block_->tree_level(), 0);
  EXPECT_EQ(result_4.state_block_->tree_level(), 1);
  EXPECT_EQ(result_5.state_block_->tree_level(), 0);
  EXPECT_EQ(result_10.state_block_->tree_level(), 1);
  EXPECT_EQ(result_15.state_block_->tree_level(), 0);
  EXPECT_EQ(result_20.state_block_->tree_level(), 0);

  EXPECT_EQ(GetLeftChildKey(result_10), KeyHandle{5});
  EXPECT_EQ(GetRightChildKey(result_10), KeyHandle{15});

  EXPECT_EQ(GetLeftChildKey(result_4), KeyHandle{3});
  EXPECT_EQ(GetRightChildKey(result_4), KeyHandle{10});

  EXPECT_FALSE(HasLeftChild(result_15));
  EXPECT_EQ(GetRightChildKey(result_15), KeyHandle{20});

  EXPECT_TRUE(IsLeaf(result_3));
  EXPECT_TRUE(IsLeaf(result_5));
  EXPECT_TRUE(IsLeaf(result_20));

  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_3));
      EXPECT_TRUE(NextIs(enumerator, result_4));
      EXPECT_TRUE(NextIs(enumerator, result_5));
      EXPECT_TRUE(NextIs(enumerator, result_10));
      EXPECT_TRUE(NextIs(enumerator, result_15));
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Inserting key 12 (as a left child of key 15). At first, this will increment
  // the level of the key 15, but then the level of the node 4(1) will be the
  // same as the level of its right grandchild (key 15). This will be repaired
  // with a split operation.
  accessor.InsertKeyBlock(MakeKeyDescriptor(12));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{12}), 1);
  KeyBlockStateSearchResult result_12 = accessor.FindKey(MakeKeyDescriptor(12));
  ASSERT_NE(result_12.state_block_, nullptr);
  EXPECT_EQ(result_12.state_block_->key_, KeyHandle{12});

  // The tree looks like this (the number in () is the level of the node):
  //      10(2)            |
  //    /       \          |
  //   4(1)      15(1)     |
  //  /   \     /    \     |
  // 3(0) 5(0) 12(0) 20(0) |
  EXPECT_EQ(result_3.state_block_->tree_level(), 0);
  EXPECT_EQ(result_4.state_block_->tree_level(), 1);
  EXPECT_EQ(result_5.state_block_->tree_level(), 0);
  EXPECT_EQ(result_10.state_block_->tree_level(), 2);
  EXPECT_EQ(result_12.state_block_->tree_level(), 0);
  EXPECT_EQ(result_15.state_block_->tree_level(), 1);
  EXPECT_EQ(result_20.state_block_->tree_level(), 0);

  EXPECT_EQ(GetLeftChildKey(result_10), KeyHandle{4});
  EXPECT_EQ(GetRightChildKey(result_10), KeyHandle{15});

  EXPECT_EQ(GetLeftChildKey(result_4), KeyHandle{3});
  EXPECT_EQ(GetRightChildKey(result_4), KeyHandle{5});

  EXPECT_EQ(GetLeftChildKey(result_15), KeyHandle{12});
  EXPECT_EQ(GetRightChildKey(result_15), KeyHandle{20});

  EXPECT_TRUE(IsLeaf(result_3));
  EXPECT_TRUE(IsLeaf(result_5));
  EXPECT_TRUE(IsLeaf(result_12));
  EXPECT_TRUE(IsLeaf(result_20));

  {
    KeyStateBlockEnumerator enumerator =
        accessor.CreateKeyStateBlockEnumerator();
    for (int i = 0; i < 5; ++i) {
      EXPECT_TRUE(NextIs(enumerator, result_3));
      EXPECT_TRUE(NextIs(enumerator, result_4));
      EXPECT_TRUE(NextIs(enumerator, result_5));
      EXPECT_TRUE(NextIs(enumerator, result_10));
      EXPECT_TRUE(NextIs(enumerator, result_12));
      EXPECT_TRUE(NextIs(enumerator, result_15));
      EXPECT_TRUE(NextIs(enumerator, result_20));
      EXPECT_FALSE(enumerator.MoveNext());
      enumerator.Reset();
    }
  }

  // Can't insert any extra blocks.
  EXPECT_FALSE(accessor.CanInsertStateBlocks(1));

  header_block->RemoveSnapshotReference(kBaseVersion, *behavior_);
}

TEST_F(HeaderBlock_Test, populate_block_index_with_keys_and_subkeys) {
  // Inserting multiple keys and subkeys into the index.
  // This checks that subkey blocks are attaching themselves to correct key
  // blocks, and the collections of subkeys belonging to different keys are not
  // interfering.
  HeaderBlock* header_block =
      HeaderBlock::CreateBlob(*behavior_, kBaseVersion, 32);
  ASSERT_NE(header_block, nullptr);
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 2);

  MutatingBlobAccessor accessor(*header_block);

  EXPECT_EQ(accessor.remaining_index_slots_capacity(), 32);

  ASSERT_TRUE(accessor.CanInsertStateBlocks(32));
  EXPECT_FALSE(accessor.CanInsertStateBlocks(33));

  // Each key will have 9 subkeys

  for (uint64_t key = 0; key < 3; ++key) {
    accessor.InsertKeyBlock(MakeKeyDescriptor(key));
    KeyBlockStateSearchResult key_block_state_search_result =
        accessor.FindKey(MakeKeyDescriptor(key));
    ASSERT_TRUE(key_block_state_search_result.state_block_);
    EXPECT_EQ(key_block_state_search_result.state_block_->key_, KeyHandle{key});
    for (uint64_t i = 0; i < 9; ++i) {
      uint64_t subkey = 123'000'000'000 + key * 100'000 + i;
      accessor.InsertSubkeyBlock(
          *behavior_, *key_block_state_search_result.state_block_, subkey);

      SubkeyBlockStateSearchResult subkey_block_state_search_result =
          accessor.FindSubkey(MakeKeyDescriptor(key), subkey);

      SubkeyStateBlock* block = subkey_block_state_search_result.state_block_;
      ASSERT_TRUE(block);
      EXPECT_EQ(block->key_, KeyHandle{key});
      EXPECT_EQ(block->subkey_, subkey);
    }
  }

  // We should be able to iterate over all keys, and over all subkeys within
  // each key.
  // Retrying several times to make sure that resetting the enumerator works.
  KeyStateBlockEnumerator key_e = accessor.CreateKeyStateBlockEnumerator();
  for (int key_retry = 0; key_retry < 3; ++key_retry) {
    for (uint64_t key = 0; key < 3; ++key) {
      ASSERT_TRUE(key_e.MoveNext());
      EXPECT_EQ(key_e.CurrentVersionBlock(), nullptr);
      EXPECT_EQ(key_e.CurrentStateBlock().key_, KeyHandle{key});
      SubkeyStateBlockEnumerator subkey_e =
          key_e.CreateSubkeyStateBlockEnumerator();
      for (int subkey_retry = 0; subkey_retry < 3; ++subkey_retry) {
        for (uint64_t i = 0; i < 9; ++i) {
          uint64_t subkey = 123'000'000'000 + key * 100'000 + i;
          EXPECT_TRUE(subkey_e.MoveNext());
          EXPECT_EQ(subkey_e.CurrentVersionBlock(), nullptr);
          EXPECT_EQ(subkey_e.CurrentStateBlock().key_, KeyHandle{key});
          EXPECT_EQ(subkey_e.CurrentStateBlock().subkey_, subkey);
        }
        EXPECT_FALSE(subkey_e.MoveNext());
        subkey_e.Reset();
      }
    }
    EXPECT_FALSE(key_e.MoveNext());
    key_e.Reset();
  }
  EXPECT_TRUE(accessor.CanInsertStateBlocks(2));
  EXPECT_FALSE(accessor.CanInsertStateBlocks(3));

  header_block->RemoveSnapshotReference(kBaseVersion, *behavior_);
}

TEST_F(HeaderBlock_Test, insertion_order_fuzzing) {
  // Tries inserting a predefined set of subkeys in various random orders,
  // expecting that the subkeys are discoverable and ordered after the
  // insertion.
  size_t kIndexCapacity = 1024;

  std::vector<uint64_t> subkeys(kIndexCapacity - 1);
  for (size_t i = 0; i < kIndexCapacity - 1; ++i) {
    subkeys[i] = 123'000'000'000ull + i;
  }

  std::mt19937 rng{std::random_device{}()};

  for (int fuzz_iteration = 0; fuzz_iteration < 300; ++fuzz_iteration) {
    HeaderBlock* header_block =
        HeaderBlock::CreateBlob(*behavior_, kBaseVersion, kIndexCapacity);
    ASSERT_NE(header_block, nullptr);

    MutatingBlobAccessor accessor(*header_block);

    EXPECT_EQ(accessor.remaining_index_slots_capacity(), kIndexCapacity);
    EXPECT_TRUE(accessor.CanInsertStateBlocks(kIndexCapacity));
    EXPECT_FALSE(accessor.CanInsertStateBlocks(kIndexCapacity + 1));

    accessor.InsertKeyBlock(MakeKeyDescriptor(5));
    KeyBlockStateSearchResult key_block_state_search_result =
        accessor.FindKey(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_block_state_search_result.state_block_);
    EXPECT_EQ(key_block_state_search_result.state_block_->key_, KeyHandle{5});

    std::shuffle(begin(subkeys), end(subkeys), rng);

    EXPECT_TRUE(accessor.CanInsertStateBlocks(subkeys.size()));
    EXPECT_FALSE(accessor.CanInsertStateBlocks(subkeys.size() + 1));

    for (size_t i = 0; i < subkeys.size(); ++i) {
      uint64_t subkey = subkeys[i];
      accessor.InsertSubkeyBlock(
          *behavior_, *key_block_state_search_result.state_block_, subkey);
    }
    // The index is full
    EXPECT_FALSE(accessor.CanInsertStateBlocks(1));

    KeyStateBlockEnumerator key_e = accessor.CreateKeyStateBlockEnumerator();
    EXPECT_TRUE(key_e.MoveNext());
    EXPECT_EQ(key_e.CurrentVersionBlock(), nullptr);
    EXPECT_EQ(key_e.CurrentStateBlock().key_, KeyHandle{5});
    SubkeyStateBlockEnumerator subkey_e =
        key_e.CreateSubkeyStateBlockEnumerator();

    const auto search_key = MakeKeyDescriptor(5);

    // Enumerator traverses through subkeys in sorted order.
    for (size_t i = 0; i < subkeys.size(); ++i) {
      const uint64_t subkey = 123'000'000'000ull + i;
      EXPECT_TRUE(subkey_e.MoveNext());
      EXPECT_EQ(subkey_e.CurrentVersionBlock(), nullptr);
      EXPECT_EQ(subkey_e.CurrentStateBlock().key_, KeyHandle{5});
      EXPECT_EQ(subkey_e.CurrentStateBlock().subkey_, subkey);

      // The subkey can also be found directly.
      const SubkeyStateBlock* block =
          accessor.FindSubkey(search_key, subkey).state_block_;
      ASSERT_TRUE(block);
      EXPECT_EQ(block->key_, KeyHandle{5});
      EXPECT_EQ(block->subkey_, subkey);
    }
    EXPECT_FALSE(subkey_e.MoveNext());
    EXPECT_FALSE(key_e.MoveNext());

    header_block->RemoveSnapshotReference(kBaseVersion, *behavior_);
  }
}

TEST_F(HeaderBlock_Test, subkey_hashes_fuzzing) {
  // Tries inserting random unique subkeys in various random orders, expecting
  // that the subkeys are discoverable and ordered after the insertion.
  // This test is likely to encounter all kinds of small hash collisions, index
  // block overflows etc.
  size_t kIndexCapacity = 256;

  std::vector<uint64_t> sorted_subkeys(kIndexCapacity - 1);
  std::vector<uint64_t> shuffled_subkeys(kIndexCapacity - 1);

  std::mt19937 rng{std::random_device{}()};
  std::uniform_int_distribution<uint64_t> distribution(0, ~0ull);

  for (int fuzz_iteration = 0; fuzz_iteration < 1000; ++fuzz_iteration) {
    for (auto& subkey : shuffled_subkeys) {
      subkey = distribution(rng);
    }
    sorted_subkeys = shuffled_subkeys;
    std::sort(begin(sorted_subkeys), end(sorted_subkeys));
    if (std::unique(begin(sorted_subkeys), end(sorted_subkeys)) !=
        end(sorted_subkeys)) {
      // The collision between subkeys is unlikely, so we simply skip the
      // iteration in case of it.
      // The test below expects all random subkeys to be unique.
      continue;
    }

    HeaderBlock* header_block =
        HeaderBlock::CreateBlob(*behavior_, kBaseVersion, kIndexCapacity);
    ASSERT_NE(header_block, nullptr);

    MutatingBlobAccessor accessor(*header_block);

    EXPECT_EQ(accessor.remaining_index_slots_capacity(), kIndexCapacity);
    EXPECT_TRUE(accessor.CanInsertStateBlocks(kIndexCapacity));
    EXPECT_FALSE(accessor.CanInsertStateBlocks(kIndexCapacity + 1));

    accessor.InsertKeyBlock(MakeKeyDescriptor(5));
    KeyBlockStateSearchResult key_block_state_search_result =
        accessor.FindKey(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_block_state_search_result.state_block_);
    EXPECT_EQ(key_block_state_search_result.state_block_->key_, KeyHandle{5});

    shuffled_subkeys = sorted_subkeys;
    std::shuffle(begin(shuffled_subkeys), end(shuffled_subkeys), rng);

    ASSERT_TRUE(accessor.CanInsertStateBlocks(shuffled_subkeys.size()));
    EXPECT_FALSE(accessor.CanInsertStateBlocks(shuffled_subkeys.size() + 1));

    for (size_t i = 0; i < shuffled_subkeys.size(); ++i) {
      uint64_t subkey = shuffled_subkeys[i];
      accessor.InsertSubkeyBlock(
          *behavior_, *key_block_state_search_result.state_block_, subkey);
    }
    // The index is full
    EXPECT_FALSE(accessor.CanInsertStateBlocks(1));

    KeyStateBlockEnumerator key_e = accessor.CreateKeyStateBlockEnumerator();
    EXPECT_TRUE(key_e.MoveNext());
    EXPECT_EQ(key_e.CurrentVersionBlock(), nullptr);
    EXPECT_EQ(key_e.CurrentStateBlock().key_, KeyHandle{5});
    SubkeyStateBlockEnumerator subkey_e =
        key_e.CreateSubkeyStateBlockEnumerator();

    const auto search_key = MakeKeyDescriptor(5);

    // Enumerator traverses through subkeys in sorted order.
    for (size_t i = 0; i < sorted_subkeys.size(); ++i) {
      const uint64_t subkey = sorted_subkeys[i];
      EXPECT_TRUE(subkey_e.MoveNext());
      EXPECT_EQ(subkey_e.CurrentVersionBlock(), nullptr);
      EXPECT_EQ(subkey_e.CurrentStateBlock().key_, KeyHandle{5});
      EXPECT_EQ(subkey_e.CurrentStateBlock().subkey_, subkey);

      // The subkey can also be found directly.
      const SubkeyStateBlock* block =
          accessor.FindSubkey(search_key, subkey).state_block_;
      ASSERT_TRUE(block);
      EXPECT_EQ(block->key_, KeyHandle{5});
      EXPECT_EQ(block->subkey_, subkey);
    }
    EXPECT_FALSE(subkey_e.MoveNext());
    EXPECT_FALSE(key_e.MoveNext());

    header_block->RemoveSnapshotReference(kBaseVersion, *behavior_);
  }
}

TEST_F(HeaderBlock_Test, empty_versions) {
  // Inserting a number of empty versions with no changes and verifying that
  // they consume the state blocks.

  HeaderBlock* header_block =
      HeaderBlock::CreateBlob(*behavior_, kBaseVersion, 7);
  ASSERT_NE(header_block, nullptr);
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  MutatingBlobAccessor accessor(*header_block);

  EXPECT_EQ(accessor.remaining_index_slots_capacity(), 7);

  // There are 64 blocks total; 1 is occupied by the header, and 1 is occupied
  // by the index. An extra block stores the refcount for the base version.
  EXPECT_EQ(header_block->data_blocks_capacity(), 62);
  EXPECT_EQ(accessor.available_data_blocks_count(), 61);

  for (int i = 0; i < 15; ++i) {
    ASSERT_TRUE(accessor.AddVersion());
  }

  // All 16 versions (including the base one, that was created by CreateBlob()
  // call) should fit into one block.
  EXPECT_EQ(accessor.available_data_blocks_count(), 61);

  // Each group of 16 versions consumes an extra block
  for (uint32_t group_id = 0; group_id < 61; ++group_id) {
    for (int i = 0; i < 16; ++i) {
      ASSERT_TRUE(accessor.AddVersion());
      EXPECT_EQ(accessor.available_data_blocks_count(), 60 - group_id);
    }
  }
  // No more versions can be added.
  EXPECT_FALSE(accessor.AddVersion());

  for (uint64_t i = 0; i < 16 * 62; ++i) {
    header_block->RemoveSnapshotReference(kBaseVersion + i, *behavior_);
  }
}

class PrepareTransaction_Test : public HeaderBlock_Test {
 public:
  PrepareTransaction_Test()
      : header_block_{HeaderBlock::CreateBlob(*behavior_, kBaseVersion, 7)},
        accessor_{*header_block_} {
    EXPECT_NE(header_block_, nullptr);
    // There are 64 blocks total; 1 is occupied by the header, and 1 is occupied
    // by the index. An extra block stores the refcount for the base version.
    EXPECT_EQ(header_block_->data_blocks_capacity(), 62);
    EXPECT_EQ(accessor_.available_data_blocks_count(), 61);

    // Adding a key and a few subkeys
    accessor_.InsertKeyBlock(MakeKeyDescriptor(5));
    KeyBlockStateSearchResult key_block_state_search_result =
        accessor_.FindKey(MakeKeyDescriptor(5));
    for (uint64_t subkey = 0; subkey < 6; ++subkey) {
      accessor_.InsertSubkeyBlock(
          *behavior_, *key_block_state_search_result.state_block_, subkey);
    }
    EXPECT_EQ(accessor_.available_data_blocks_count(), 54);
  }

 protected:
  static bool KeyMatches(const KeyBlockStateSearchResult& search_result,
                         uint64_t key) noexcept {
    return search_result.state_block_ &&
           search_result.state_block_->key_ == KeyHandle{key};
  }

  uint32_t GetSubkeyCountForVersion(uint64_t version) noexcept {
    return accessor_.FindKey(version, MakeKeyDescriptor(5)).value();
  }

  static bool KeySubkeyMatch(const SubkeyBlockStateSearchResult& search_result,
                             uint64_t key,
                             uint64_t subkey) noexcept {
    return search_result.state_block_ &&
           search_result.state_block_->key_ == KeyHandle{key} &&
           search_result.state_block_->subkey_ == subkey;
  }

  bool HasPayload(uint64_t version, uint64_t subkey) noexcept {
    return accessor_.FindSubkey(version, MakeKeyDescriptor(5), subkey)
        .value()
        .has_payload();
  }

  VersionedPayloadHandle GetPayload(uint64_t version, uint64_t subkey) {
    return accessor_.FindSubkey(version, MakeKeyDescriptor(5), subkey).value();
  }

  HeaderBlock* header_block_;
  MutatingBlobAccessor accessor_;
};

TEST_F(PrepareTransaction_Test, inserting_subkey_version) {
  uint64_t subkey = 2;
  SubkeyBlockStateSearchResult search_result =
      accessor_.FindSubkey(MakeKeyDescriptor(5), subkey);

  ASSERT_TRUE(
      accessor_.ReserveSpaceForTransaction(search_result, kBaseVersion, true));
  EXPECT_TRUE(KeySubkeyMatch(search_result, 5, subkey));
  EXPECT_FALSE(search_result.version_block_);

  // The operation didn't consume any version blocks
  EXPECT_EQ(accessor_.available_data_blocks_count(), 54);

  // This takes the ownership of the payload, and the reference should be
  // released when the block is destroyed.
  search_result.state_block_->Push(kBaseVersion, MakePayload(42));

  // The subkey doesn't exist before this version
  EXPECT_FALSE(HasPayload(0, subkey));
  EXPECT_FALSE(HasPayload(kBaseVersion - 1, subkey));

  // The subkey exists after this version.
  for (uint64_t i = 0; i < 10; ++i) {
    ASSERT_TRUE(HasPayload(kBaseVersion + i, subkey));
    EXPECT_EQ(GetPayload(kBaseVersion + i, subkey).payload(),
              PayloadHandle{42});
  }

  ASSERT_TRUE(accessor_.AddVersion());

  // Now trying without a precondition, but attempting to write the payload that
  // is already there.
  EXPECT_TRUE(accessor_.ReserveSpaceForTransaction(search_result,
                                                   kBaseVersion + 1, false));
  // The operation didn't consume any version blocks
  EXPECT_EQ(accessor_.available_data_blocks_count(), 54);
  search_result.state_block_->Push(kBaseVersion + 1, {});

  // The subkey doesn't exist before the first version
  EXPECT_FALSE(HasPayload(kBaseVersion - 1, subkey));
  // Payload 42 is still visible to the base version
  ASSERT_TRUE(HasPayload(kBaseVersion, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion, subkey).payload(), PayloadHandle{42});
  // But in the next version it's deleted
  EXPECT_FALSE(HasPayload(kBaseVersion + 1, subkey));

  // This version should allocate a new version block (with both existing
  // versions and enough space for one more version).
  ASSERT_TRUE(accessor_.AddVersion());
  EXPECT_EQ(accessor_.available_data_blocks_count(), 54);
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(search_result,
                                                   kBaseVersion + 2, true));
  // The operation consumed one version block.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 53);

  EXPECT_TRUE(search_result.version_block_);

  search_result.version_block_->Push(kBaseVersion + 2, MakePayload(43));

  // The subkey doesn't exist before the first version.
  EXPECT_FALSE(HasPayload(kBaseVersion - 1, subkey));
  // Payload 42 is duplicated in the new version block.
  ASSERT_TRUE(HasPayload(kBaseVersion, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion, subkey).payload(), PayloadHandle{42});
  // The deletion marker for the next version is also duplicated.
  EXPECT_FALSE(HasPayload(kBaseVersion + 1, subkey));
  // Newly published version is visible.
  ASSERT_TRUE(HasPayload(kBaseVersion + 2, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion + 2, subkey).payload(), PayloadHandle{43});

  // This version should fit into existing version block.
  ASSERT_TRUE(accessor_.AddVersion());
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(search_result,
                                                   kBaseVersion + 3, false));

  // The operation didn't consume a version block.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 53);

  EXPECT_TRUE(search_result.version_block_);

  search_result.version_block_->Push(kBaseVersion + 3, {});

  EXPECT_FALSE(HasPayload(kBaseVersion - 1, subkey));
  ASSERT_TRUE(HasPayload(kBaseVersion, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion, subkey).payload(), PayloadHandle{42});
  EXPECT_FALSE(HasPayload(kBaseVersion + 1, subkey));
  ASSERT_TRUE(HasPayload(kBaseVersion + 2, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion + 2, subkey).payload(), PayloadHandle{43});
  EXPECT_FALSE(HasPayload(kBaseVersion + 3, subkey));

  // Forgetting about the version where the payload was deleted the first time.
  // This will influence which versions will survive the reallocation of the
  // version block.
  header_block_->RemoveSnapshotReference(kBaseVersion + 1, *behavior_);

  ASSERT_TRUE(accessor_.AddVersion());
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(search_result,
                                                   kBaseVersion + 4, true));
  // The operation consumed two new version blocks.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 51);

  EXPECT_TRUE(search_result.version_block_);

  search_result.version_block_->Push(kBaseVersion + 4, MakePayload(44));

  EXPECT_FALSE(HasPayload(kBaseVersion - 1, subkey));
  ASSERT_TRUE(HasPayload(kBaseVersion, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion, subkey).payload(), PayloadHandle{42});

  // The information about this version did not migrate to the new version block
  // (because we removed the reference to this version above).
  // Because of that, we see the previous version instead of a deletion marker.
  // In the actual use case scenario we would never perform a search for an
  // unreferenced version, but here this checks the reallocation strategy.
  ASSERT_TRUE(HasPayload(kBaseVersion + 1, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion + 1, subkey).payload(), PayloadHandle{42});

  ASSERT_TRUE(HasPayload(kBaseVersion + 2, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion + 2, subkey).payload(), PayloadHandle{43});
  EXPECT_FALSE(HasPayload(kBaseVersion + 3, subkey));

  ASSERT_TRUE(HasPayload(kBaseVersion + 4, subkey));
  EXPECT_EQ(GetPayload(kBaseVersion + 4, subkey).payload(), PayloadHandle{44});

  header_block_->RemoveSnapshotReference(kBaseVersion, *behavior_);
  header_block_->RemoveSnapshotReference(kBaseVersion + 2, *behavior_);
  header_block_->RemoveSnapshotReference(kBaseVersion + 3, *behavior_);
  header_block_->RemoveSnapshotReference(kBaseVersion + 4, *behavior_);
}

TEST_F(PrepareTransaction_Test, inserting_key_versions) {
  // First, pushing 3 subkey counts (all of them should fit into the in-place
  // storage within the state block)
  for (uint32_t i = 0; i < 3; ++i) {
    KeyBlockStateSearchResult search_result =
        accessor_.FindKey(MakeKeyDescriptor(5));
    uint32_t new_count = 9000 + i;
    ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(search_result));
    EXPECT_TRUE(KeyMatches(search_result, 5));
    EXPECT_FALSE(search_result.version_block_);

    // The operation didn't consume any version blocks
    EXPECT_EQ(accessor_.available_data_blocks_count(), 54);

    ASSERT_TRUE(search_result.state_block_->HasFreeInPlaceSlots());
    search_result.state_block_->PushSubkeysCount(VersionOffset{i}, new_count);

    // There are no subkeys before the first published version
    EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion - 1), 0);

    // All inserted versions are visible
    for (uint32_t j = 0; j <= i; ++j) {
      EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + j), 9000 + j);
    }
    ASSERT_TRUE(accessor_.AddVersion());
  }

  // The next 4 versions will use a version block.
  // (the existing 3 versions will be copied there since they are referenced).
  for (uint32_t i = 3; i < 7; ++i) {
    KeyBlockStateSearchResult search_result =
        accessor_.FindKey(MakeKeyDescriptor(5));
    const uint32_t new_count = 9000 + i;
    ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(search_result));
    EXPECT_TRUE(KeyMatches(search_result, 5));
    ASSERT_TRUE(search_result.version_block_);

    // All versions are in the same version block.
    EXPECT_EQ(accessor_.available_data_blocks_count(), 53);

    ASSERT_TRUE(search_result.version_block_->HasEmptySlots());
    search_result.version_block_->PushSubkeysCount(VersionOffset{i}, new_count);

    // There are no subkeys before the first published version
    EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion - 1), 0);

    // All inserted versions are visible
    for (uint32_t j = 0; j <= i; ++j) {
      EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + j), 9000 + j);
    }
    EXPECT_TRUE(accessor_.AddVersion());
  }

  // Dereferencing a single version.
  header_block_->RemoveSnapshotReference(kBaseVersion + 2, *behavior_);

  KeyBlockStateSearchResult search_result =
      accessor_.FindKey(MakeKeyDescriptor(5));
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(search_result));
  EXPECT_TRUE(KeyMatches(search_result, 5));
  ASSERT_TRUE(search_result.version_block_);

  // Two new blocks were allocated, since 6 out of 7 previous versions are still
  // alive and have to be preserved.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 51);

  EXPECT_EQ(search_result.version_block_->capacity(), 15);
  EXPECT_EQ(search_result.version_block_->size_relaxed(), 6);
  search_result.version_block_->PushSubkeysCount(VersionOffset{7}, 9007);
  EXPECT_EQ(search_result.version_block_->size_relaxed(), 7);

  // There are no subkeys before the first published version
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion - 1), 0);
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion), 9000);
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 1), 9001);
  // This version was unreferenced above and didn't migrate into the new version
  // blocks, so the storage doesn't know that at some the value was 9002.
  // In the actual use case scenario we would never perform a search for an
  // unreferenced version, but here this checks the reallocation strategy.
  // 9001 here is not a typo:
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 2), 9001);

  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 3), 9003);
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 4), 9004);
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 5), 9005);
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 6), 9006);
  EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + 7), 9007);

  for (int i = 0; i < 8; ++i) {
    // Version kBaseVersion + 2 was unreferenced during the test
    if (i != 2) {
      header_block_->RemoveSnapshotReference(kBaseVersion + i, *behavior_);
    }
  }
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
