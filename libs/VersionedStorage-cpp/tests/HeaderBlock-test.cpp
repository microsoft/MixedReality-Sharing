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

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

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

  static bool HasLeftChild(const KeyStateView& key_state_view) noexcept {
    assert(!key_state_view.state_block_->is_scratch_buffer_mode());
    return key_state_view.state_block_->left_tree_child_ !=
           DataBlockLocation::kInvalid;
  }

  static bool HasRightChild(const KeyStateView& key_state_view) noexcept {
    assert(!key_state_view.state_block_->is_scratch_buffer_mode());
    return key_state_view.state_block_->right_tree_child_ !=
           DataBlockLocation::kInvalid;
  }
  static bool IsLeaf(const KeyStateView& key_state_view) noexcept {
    assert(!key_state_view.state_block_->is_scratch_buffer_mode());
    return key_state_view.state_block_->left_tree_child_ ==
               DataBlockLocation::kInvalid &&
           key_state_view.state_block_->right_tree_child_ ==
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
  auto GetLeftChildKey = [&](const KeyStateView& key_state_view) noexcept {
    if (!HasLeftChild(key_state_view))
      return KeyHandle{~0ull};
    DataBlockLocation left = key_state_view.state_block_->left_tree_child_;
    return accessor.GetBlockAt<KeyStateBlock>(left).key_;
  };
  auto GetRightChildKey = [&](const KeyStateView& key_state_view) noexcept {
    if (!HasRightChild(key_state_view))
      return KeyHandle{~0ull};
    DataBlockLocation right = key_state_view.state_block_->right_tree_child_;
    return accessor.GetBlockAt<KeyStateBlock>(right).key_;
  };

  {
    KeyStateAndIndexView view =
        accessor.FindKeyStateAndIndex(MakeKeyDescriptor(20));
    EXPECT_EQ(view.index_block_slot_, nullptr);
    EXPECT_EQ(view.state_block_, nullptr);
    EXPECT_EQ(view.version_block_, nullptr);
  }

  // Inserting key 20. This is the simplest case, since there is no other keys,
  // and thus the key will be inserted as a head of the list and the root of the
  // tree (of keys).
  accessor.InsertKeyBlock(MakeKeyDescriptor(20));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{20}), 1);
  KeyStateAndIndexView view_20 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(20));
  ASSERT_NE(view_20.state_block_, nullptr);
  EXPECT_EQ(view_20.key(), KeyHandle{20});
  EXPECT_TRUE(IsLeaf(view_20));

  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
  }

  // Inserting key 10. It's less than the previous one, so it should be inserted
  // as a left child of the key 20. Then the AA-tree invariant should become
  // broken, and the skew operation will be performed, repairing the tree.
  // The new node (10) will become the new root.
  accessor.InsertKeyBlock(MakeKeyDescriptor(10));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{10}), 1);
  KeyStateAndIndexView view_10 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(10));
  ASSERT_NE(view_10.state_block_, nullptr);
  EXPECT_EQ(view_10.key(), KeyHandle{10});

  // The tree looks like this (the number in () is the level of the node):
  // 10(0)    |
  //   \      |
  //    20(0) |
  EXPECT_EQ(view_10.state_block_->tree_level(), 0);
  EXPECT_EQ(view_20.state_block_->tree_level(), 0);

  EXPECT_FALSE(HasLeftChild(view_10));
  EXPECT_EQ(GetRightChildKey(view_10), KeyHandle{20});
  EXPECT_TRUE(IsLeaf(view_20));

  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_10.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
  }

  // Inserting key 5. It's less than the root, and thus should be
  // inserted as the left child. It will break the invariant, but since
  // the root has the right child, the invariant can't be repaired by
  // skewing the tree. Instead, the level of the root will be
  // incremented.
  accessor.InsertKeyBlock(MakeKeyDescriptor(5));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{5}), 1);
  KeyStateAndIndexView view_5 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(5));
  ASSERT_NE(view_5.state_block_, nullptr);
  EXPECT_EQ(view_5.key(), KeyHandle{5});

  // The tree looks like this (the number in () is the level of the node):
  //    10(1)    |
  //   /   \     |
  // 5(0)  20(0) |
  EXPECT_EQ(view_5.state_block_->tree_level(), 0);
  EXPECT_EQ(view_10.state_block_->tree_level(), 1);
  EXPECT_EQ(view_20.state_block_->tree_level(), 0);
  EXPECT_EQ(GetLeftChildKey(view_10), KeyHandle{5});
  EXPECT_EQ(GetRightChildKey(view_10), KeyHandle{20});
  EXPECT_TRUE(IsLeaf(view_5));
  EXPECT_TRUE(IsLeaf(view_20));

  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_5.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_10.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
  }

  // Inserting key 4. It's less than the root, and the left child is
  // already present. Therefore the insertion should recurse into adding
  // it as a child to 5. But then the invariant will become broken, and
  // the subtree will become skewed.
  accessor.InsertKeyBlock(MakeKeyDescriptor(4));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{4}), 1);
  KeyStateAndIndexView view_4 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(4));
  ASSERT_NE(view_4.state_block_, nullptr);
  EXPECT_EQ(view_4.key(), KeyHandle{4});

  // The tree looks like this (the number in () is the level of the node):
  //    10(1)    |
  //   /   \     |
  // 4(0)  20(0) |
  //   \         |
  //   5(0)      |
  EXPECT_EQ(view_4.state_block_->tree_level(), 0);
  EXPECT_EQ(view_5.state_block_->tree_level(), 0);
  EXPECT_EQ(view_10.state_block_->tree_level(), 1);
  EXPECT_EQ(view_20.state_block_->tree_level(), 0);
  EXPECT_EQ(GetLeftChildKey(view_10), KeyHandle{4});
  EXPECT_EQ(GetRightChildKey(view_10), KeyHandle{20});
  EXPECT_FALSE(HasLeftChild(view_4));
  EXPECT_EQ(GetRightChildKey(view_4), KeyHandle{5});
  EXPECT_TRUE(IsLeaf(view_5));
  EXPECT_TRUE(IsLeaf(view_20));
  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_4.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_5.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_10.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
  }

  // Inserting key 3. It should break the invariant twice. The first
  // time it will be repaired by incrementing the level, the second time
  // the skew operation will be performed. This test should validate
  // that children are properly preserved during the skew operation.
  accessor.InsertKeyBlock(MakeKeyDescriptor(3));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{3}), 1);
  KeyStateAndIndexView view_3 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(3));
  ASSERT_NE(view_3.state_block_, nullptr);
  EXPECT_EQ(view_3.key(), KeyHandle{3});

  // The tree looks like this (the number in () is the level of the node):
  //    4(1)         |
  //   /   \         |
  // 3(0)  10(1)     |
  //       /  \      |
  //     5(0)  20(0) |
  EXPECT_EQ(view_3.state_block_->tree_level(), 0);
  EXPECT_EQ(view_4.state_block_->tree_level(), 1);
  EXPECT_EQ(view_5.state_block_->tree_level(), 0);
  EXPECT_EQ(view_10.state_block_->tree_level(), 1);
  EXPECT_EQ(view_20.state_block_->tree_level(), 0);

  EXPECT_EQ(GetLeftChildKey(view_10), KeyHandle{5});
  EXPECT_EQ(GetRightChildKey(view_10), KeyHandle{20});

  EXPECT_EQ(GetLeftChildKey(view_4), KeyHandle{3});
  EXPECT_EQ(GetRightChildKey(view_4), KeyHandle{10});

  EXPECT_TRUE(IsLeaf(view_3));
  EXPECT_TRUE(IsLeaf(view_5));
  EXPECT_TRUE(IsLeaf(view_20));

  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_3.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_4.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_5.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_10.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
  }

  // Inserting key 15. It should recurse into the right subtree and be inserted
  // with one skew.
  accessor.InsertKeyBlock(MakeKeyDescriptor(15));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{15}), 1);
  KeyStateAndIndexView view_15 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(15));
  ASSERT_NE(view_15.state_block_, nullptr);
  EXPECT_EQ(view_15.key(), KeyHandle{15});

  // The tree looks like this (the number in () is the level of the node):
  //    4(1)          |
  //   /   \          |
  // 3(0)  10(1)      |
  //       /  \       |
  //     5(0)  15(0)  |
  //            \     |
  //            20(0) |
  EXPECT_EQ(view_3.state_block_->tree_level(), 0);
  EXPECT_EQ(view_4.state_block_->tree_level(), 1);
  EXPECT_EQ(view_5.state_block_->tree_level(), 0);
  EXPECT_EQ(view_10.state_block_->tree_level(), 1);
  EXPECT_EQ(view_15.state_block_->tree_level(), 0);
  EXPECT_EQ(view_20.state_block_->tree_level(), 0);

  EXPECT_EQ(GetLeftChildKey(view_10), KeyHandle{5});
  EXPECT_EQ(GetRightChildKey(view_10), KeyHandle{15});

  EXPECT_EQ(GetLeftChildKey(view_4), KeyHandle{3});
  EXPECT_EQ(GetRightChildKey(view_4), KeyHandle{10});

  EXPECT_FALSE(HasLeftChild(view_15));
  EXPECT_EQ(GetRightChildKey(view_15), KeyHandle{20});

  EXPECT_TRUE(IsLeaf(view_3));
  EXPECT_TRUE(IsLeaf(view_5));
  EXPECT_TRUE(IsLeaf(view_20));

  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_3.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_4.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_5.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_10.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_15.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
  }

  // Inserting key 12 (as a left child of key 15). At first, this will increment
  // the level of the key 15, but then the level of the node 4(1) will be the
  // same as the level of its right grandchild (key 15). This will be repaired
  // with a split operation.
  accessor.InsertKeyBlock(MakeKeyDescriptor(12));
  EXPECT_EQ(behavior_->GetKeyReferenceCount(KeyHandle{12}), 1);
  KeyStateAndIndexView view_12 =
      accessor.FindKeyStateAndIndex(MakeKeyDescriptor(12));
  ASSERT_NE(view_12.state_block_, nullptr);
  EXPECT_EQ(view_12.key(), KeyHandle{12});

  // The tree looks like this (the number in () is the level of the node):
  //      10(2)            |
  //    /       \          |
  //   4(1)      15(1)     |
  //  /   \     /    \     |
  // 3(0) 5(0) 12(0) 20(0) |
  EXPECT_EQ(view_3.state_block_->tree_level(), 0);
  EXPECT_EQ(view_4.state_block_->tree_level(), 1);
  EXPECT_EQ(view_5.state_block_->tree_level(), 0);
  EXPECT_EQ(view_10.state_block_->tree_level(), 2);
  EXPECT_EQ(view_12.state_block_->tree_level(), 0);
  EXPECT_EQ(view_15.state_block_->tree_level(), 1);
  EXPECT_EQ(view_20.state_block_->tree_level(), 0);

  EXPECT_EQ(GetLeftChildKey(view_10), KeyHandle{4});
  EXPECT_EQ(GetRightChildKey(view_10), KeyHandle{15});

  EXPECT_EQ(GetLeftChildKey(view_4), KeyHandle{3});
  EXPECT_EQ(GetRightChildKey(view_4), KeyHandle{5});

  EXPECT_EQ(GetLeftChildKey(view_15), KeyHandle{12});
  EXPECT_EQ(GetRightChildKey(view_15), KeyHandle{20});

  EXPECT_TRUE(IsLeaf(view_3));
  EXPECT_TRUE(IsLeaf(view_5));
  EXPECT_TRUE(IsLeaf(view_12));
  EXPECT_TRUE(IsLeaf(view_20));

  {
    auto it = accessor.begin();
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_3.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_4.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_5.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_10.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_12.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_15.state_block_);
    ++it;
    ASSERT_NE(it, accessor.end());
    EXPECT_EQ(it->version_block_, nullptr);
    EXPECT_EQ(it->state_block_, view_20.state_block_);
    ++it;
    EXPECT_EQ(it, accessor.end());
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
    KeyStateAndIndexView key_state_view =
        accessor.FindKeyStateAndIndex(MakeKeyDescriptor(key));
    ASSERT_TRUE(key_state_view.state_block_);
    EXPECT_EQ(key_state_view.key(), KeyHandle{key});
    for (uint64_t i = 0; i < 9; ++i) {
      uint64_t subkey = 123'000'000'000 + key * 100'000 + i;
      accessor.InsertSubkeyBlock(*behavior_, *key_state_view.state_block_,
                                 subkey);

      SubkeyStateAndIndexView subkey_state_view =
          accessor.FindSubkeyStateAndIndex(MakeKeyDescriptor(key), subkey);

      SubkeyStateBlock* block = subkey_state_view.state_block_;
      ASSERT_TRUE(block);
      EXPECT_EQ(block->key_, KeyHandle{key});
      EXPECT_EQ(block->subkey_, subkey);
    }
  }

  // We should be able to iterate over all keys, and over all subkeys within
  // each key.
  KeyBlockIterator key_it = accessor.begin();
  for (uint64_t key = 0; key < 3; ++key) {
    ASSERT_NE(key_it, accessor.end());
    EXPECT_EQ(key_it->version_block_, nullptr);
    EXPECT_EQ(key_it->key(), KeyHandle{key});

    SubkeyBlockIterator subkey_it = accessor.GetSubkeys(*key_it).begin();
    for (uint64_t i = 0; i < 9; ++i) {
      uint64_t subkey = 123'000'000'000 + key * 100'000 + i;
      ASSERT_NE(subkey_it, SubkeyBlockIterator::End{});
      EXPECT_EQ(subkey_it->version_block_, nullptr);
      EXPECT_EQ(subkey_it->key(), KeyHandle{key});
      EXPECT_EQ(subkey_it->subkey(), subkey);
      ++subkey_it;
    }
    EXPECT_EQ(subkey_it, SubkeyBlockIterator::End{});
    ++key_it;
  }
  ASSERT_EQ(key_it, accessor.end());
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
    KeyStateAndIndexView key_state_view =
        accessor.FindKeyStateAndIndex(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_state_view.state_block_);
    EXPECT_EQ(key_state_view.key(), KeyHandle{5});

    std::shuffle(begin(subkeys), end(subkeys), rng);

    EXPECT_TRUE(accessor.CanInsertStateBlocks(subkeys.size()));
    EXPECT_FALSE(accessor.CanInsertStateBlocks(subkeys.size() + 1));

    for (size_t i = 0; i < subkeys.size(); ++i) {
      uint64_t subkey = subkeys[i];
      accessor.InsertSubkeyBlock(*behavior_, *key_state_view.state_block_,
                                 subkey);
    }
    // The index is full
    EXPECT_FALSE(accessor.CanInsertStateBlocks(1));

    KeyBlockIterator key_it = accessor.begin();
    ASSERT_NE(key_it, accessor.end());
    EXPECT_EQ(key_it->version_block_, nullptr);
    EXPECT_EQ(key_it->key(), KeyHandle{5});

    SubkeyBlockIterator subkey_it = accessor.GetSubkeys(*key_it).begin();
    const auto search_key = MakeKeyDescriptor(5);

    // Iterator traverses through subkeys in sorted order.
    for (size_t i = 0; i < subkeys.size(); ++i) {
      const uint64_t subkey = 123'000'000'000ull + i;
      ASSERT_NE(subkey_it, SubkeyBlockIterator::End{});
      EXPECT_EQ(subkey_it->version_block_, nullptr);
      EXPECT_EQ(subkey_it->key(), KeyHandle{5});
      EXPECT_EQ(subkey_it->subkey(), subkey);

      // The subkey can also be found directly.
      const SubkeyStateBlock* block =
          accessor.FindSubkeyState(search_key, subkey).state_block_;
      ASSERT_TRUE(block);
      EXPECT_EQ(block->key_, KeyHandle{5});
      EXPECT_EQ(block->subkey_, subkey);
      ++subkey_it;
    }
    EXPECT_EQ(subkey_it, SubkeyBlockIterator::End{});
    ++key_it;
    EXPECT_EQ(key_it, accessor.end());
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
    KeyStateAndIndexView key_state_view =
        accessor.FindKeyStateAndIndex(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_state_view.state_block_);
    EXPECT_EQ(key_state_view.key(), KeyHandle{5});

    ASSERT_TRUE(accessor.CanInsertStateBlocks(shuffled_subkeys.size()));
    EXPECT_FALSE(accessor.CanInsertStateBlocks(shuffled_subkeys.size() + 1));

    for (size_t i = 0; i < shuffled_subkeys.size(); ++i) {
      uint64_t subkey = shuffled_subkeys[i];
      accessor.InsertSubkeyBlock(*behavior_, *key_state_view.state_block_,
                                 subkey);
    }
    // The index is full
    EXPECT_FALSE(accessor.CanInsertStateBlocks(1));

    KeyBlockIterator key_it = accessor.begin();
    ASSERT_NE(key_it, accessor.end());
    EXPECT_EQ(key_it->version_block_, nullptr);
    EXPECT_EQ(key_it->key(), KeyHandle{5});
    SubkeyBlockIterator subkey_it = accessor.GetSubkeys(*key_it).begin();
    const auto search_key = MakeKeyDescriptor(5);

    // Iterator traverses through subkeys in sorted order.
    for (size_t i = 0; i < sorted_subkeys.size(); ++i) {
      const uint64_t subkey = sorted_subkeys[i];
      ASSERT_NE(subkey_it, SubkeyBlockIterator::End{});
      EXPECT_EQ(subkey_it->version_block_, nullptr);
      EXPECT_EQ(subkey_it->key(), KeyHandle{5});
      EXPECT_EQ(subkey_it->subkey(), subkey);

      // The subkey can also be found directly.
      const SubkeyStateBlock* block =
          accessor.FindSubkeyState(search_key, subkey).state_block_;
      ASSERT_TRUE(block);
      EXPECT_EQ(block->key_, KeyHandle{5});
      EXPECT_EQ(block->subkey_, subkey);
      ++subkey_it;
    }
    EXPECT_EQ(subkey_it, SubkeyBlockIterator::End{});
    ++key_it;
    EXPECT_EQ(key_it, accessor.end());
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
    KeyStateView key_state_view = accessor_.FindKeyState(MakeKeyDescriptor(5));
    for (uint64_t subkey = 0; subkey < 6; ++subkey) {
      accessor_.InsertSubkeyBlock(*behavior_, *key_state_view.state_block_,
                                  subkey);
    }
    EXPECT_EQ(accessor_.available_data_blocks_count(), 54);
  }

 protected:
  static bool KeyMatches(const KeyStateView& key_state_view,
                         uint64_t key) noexcept {
    return key_state_view.state_block_ &&
           key_state_view.key() == KeyHandle{key};
  }

  uint32_t GetSubkeyCountForVersion(uint64_t version) noexcept {
    if (KeyStateView view = accessor_.FindKeyState(MakeKeyDescriptor(5)))
      return view.GetSubkeysCount(MakeVersionOffset(version, kBaseVersion));

    return 0;
  }

  static bool KeySubkeyMatch(const SubkeyStateView& subkey_state_view,
                             uint64_t key,
                             uint64_t subkey) noexcept {
    return subkey_state_view.state_block_ &&
           subkey_state_view.key() == KeyHandle{key} &&
           subkey_state_view.subkey() == subkey;
  }

  VersionedPayloadHandle Find(uint64_t version, uint64_t subkey) {
    if (SubkeyStateView view =
            accessor_.FindSubkeyState(MakeKeyDescriptor(5), subkey))
      return view.GetPayload(version);
    return {};
  }

  HeaderBlock* header_block_;
  MutatingBlobAccessor accessor_;
};

TEST_F(PrepareTransaction_Test, inserting_subkey_version) {
  uint64_t subkey = 2;
  SubkeyStateAndIndexView subkey_state_view =
      accessor_.FindSubkeyStateAndIndex(MakeKeyDescriptor(5), subkey);

  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(subkey_state_view,
                                                   kBaseVersion, true));
  EXPECT_TRUE(KeySubkeyMatch(subkey_state_view, 5, subkey));
  EXPECT_FALSE(subkey_state_view.version_block_);

  // The operation didn't consume any version blocks
  EXPECT_EQ(accessor_.available_data_blocks_count(), 54);

  // This takes the ownership of the payload, and the reference should be
  // released when the block is destroyed.
  subkey_state_view.state_block_->PushFromWriterThread(kBaseVersion,
                                                       MakePayload(42));

  // The subkey doesn't exist before this version
  EXPECT_FALSE(Find(0, subkey));
  EXPECT_FALSE(Find(kBaseVersion - 1, subkey));

  // The subkey exists after this version.
  for (uint64_t i = 0; i < 10; ++i) {
    ASSERT_TRUE(Find(kBaseVersion + i, subkey));
    EXPECT_EQ(Find(kBaseVersion + i, subkey).payload(), PayloadHandle{42});
    EXPECT_EQ(Find(kBaseVersion + i, subkey).version(), kBaseVersion);
  }

  ASSERT_TRUE(accessor_.AddVersion());

  // Now trying without a precondition, but attempting to write the payload that
  // is already there.
  EXPECT_TRUE(accessor_.ReserveSpaceForTransaction(subkey_state_view,
                                                   kBaseVersion + 1, false));
  // The operation didn't consume any version blocks
  EXPECT_EQ(accessor_.available_data_blocks_count(), 54);
  subkey_state_view.state_block_->PushFromWriterThread(kBaseVersion + 1, {});

  // The subkey doesn't exist before the first version
  EXPECT_FALSE(Find(kBaseVersion - 1, subkey));
  // Payload 42 is still visible to the base version
  ASSERT_TRUE(Find(kBaseVersion, subkey));
  EXPECT_EQ(Find(kBaseVersion, subkey).payload(), PayloadHandle{42});
  EXPECT_EQ(Find(kBaseVersion, subkey).version(), kBaseVersion);

  // But in the next version it's deleted
  EXPECT_FALSE(Find(kBaseVersion + 1, subkey));

  // This version should allocate a new version block (with both existing
  // versions and enough space for one more version).
  ASSERT_TRUE(accessor_.AddVersion());
  EXPECT_EQ(accessor_.available_data_blocks_count(), 54);
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(subkey_state_view,
                                                   kBaseVersion + 2, true));
  // The operation consumed one version block.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 53);

  EXPECT_TRUE(subkey_state_view.version_block_);

  subkey_state_view.version_block_->PushFromWriterThread(kBaseVersion + 2,
                                                         MakePayload(43));

  // The subkey doesn't exist before the first version.
  EXPECT_FALSE(Find(kBaseVersion - 1, subkey));
  // Payload 42 is duplicated in the new version block.
  ASSERT_TRUE(Find(kBaseVersion, subkey));
  EXPECT_EQ(Find(kBaseVersion, subkey).payload(), PayloadHandle{42});
  EXPECT_EQ(Find(kBaseVersion, subkey).version(), kBaseVersion);
  // The deletion marker for the next version is also duplicated.
  EXPECT_FALSE(Find(kBaseVersion + 1, subkey));
  // Newly published version is visible.
  ASSERT_TRUE(Find(kBaseVersion + 2, subkey));
  EXPECT_EQ(Find(kBaseVersion + 2, subkey).payload(), PayloadHandle{43});
  EXPECT_EQ(Find(kBaseVersion + 2, subkey).version(), kBaseVersion + 2);

  // This version should fit into existing version block.
  ASSERT_TRUE(accessor_.AddVersion());
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(subkey_state_view,
                                                   kBaseVersion + 3, false));

  // The operation didn't consume a version block.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 53);

  EXPECT_TRUE(subkey_state_view.version_block_);

  subkey_state_view.version_block_->PushFromWriterThread(kBaseVersion + 3, {});

  EXPECT_FALSE(Find(kBaseVersion - 1, subkey));
  ASSERT_TRUE(Find(kBaseVersion, subkey));
  EXPECT_EQ(Find(kBaseVersion, subkey).payload(), PayloadHandle{42});
  EXPECT_EQ(Find(kBaseVersion, subkey).version(), kBaseVersion);
  EXPECT_FALSE(Find(kBaseVersion + 1, subkey));
  ASSERT_TRUE(Find(kBaseVersion + 2, subkey));
  EXPECT_EQ(Find(kBaseVersion + 2, subkey).payload(), PayloadHandle{43});
  EXPECT_EQ(Find(kBaseVersion + 2, subkey).version(), kBaseVersion + 2);
  EXPECT_FALSE(Find(kBaseVersion + 3, subkey));

  // Forgetting about the version where the payload was deleted the first time.
  // This will influence which versions will survive the reallocation of the
  // version block.
  header_block_->RemoveSnapshotReference(kBaseVersion + 1, *behavior_);

  ASSERT_TRUE(accessor_.AddVersion());
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(subkey_state_view,
                                                   kBaseVersion + 4, true));
  // The operation consumed two new version blocks.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 51);

  EXPECT_TRUE(subkey_state_view.version_block_);

  subkey_state_view.version_block_->PushFromWriterThread(kBaseVersion + 4,
                                                         MakePayload(44));

  EXPECT_FALSE(Find(kBaseVersion - 1, subkey));
  ASSERT_TRUE(Find(kBaseVersion, subkey));
  EXPECT_EQ(Find(kBaseVersion, subkey).payload(), PayloadHandle{42});
  EXPECT_EQ(Find(kBaseVersion, subkey).version(), kBaseVersion);

  // The information about this version did not migrate to the new version block
  // (because we removed the reference to this version above).
  // Because of that, we see the previous version instead of a deletion marker.
  // In the actual use case scenario we would never perform a search for an
  // unreferenced version, but here this checks the reallocation strategy.
  ASSERT_TRUE(Find(kBaseVersion + 1, subkey));
  EXPECT_EQ(Find(kBaseVersion + 1, subkey).payload(), PayloadHandle{42});
  // Note: inserted in the previous version (the deletion marker that was here
  // instead is now missing).
  EXPECT_EQ(Find(kBaseVersion + 1, subkey).version(), kBaseVersion);

  ASSERT_TRUE(Find(kBaseVersion + 2, subkey));
  EXPECT_EQ(Find(kBaseVersion + 2, subkey).payload(), PayloadHandle{43});
  EXPECT_EQ(Find(kBaseVersion + 2, subkey).version(), kBaseVersion + 2);
  EXPECT_FALSE(Find(kBaseVersion + 3, subkey));

  ASSERT_TRUE(Find(kBaseVersion + 4, subkey));
  EXPECT_EQ(Find(kBaseVersion + 4, subkey).payload(), PayloadHandle{44});
  EXPECT_EQ(Find(kBaseVersion + 4, subkey).version(), kBaseVersion + 4);

  header_block_->RemoveSnapshotReference(kBaseVersion, *behavior_);
  header_block_->RemoveSnapshotReference(kBaseVersion + 2, *behavior_);
  header_block_->RemoveSnapshotReference(kBaseVersion + 3, *behavior_);
  header_block_->RemoveSnapshotReference(kBaseVersion + 4, *behavior_);
}

TEST_F(PrepareTransaction_Test, inserting_key_versions) {
  // First, pushing 3 subkey counts (all of them should fit into the in-place
  // storage within the state block)
  for (uint32_t i = 0; i < 3; ++i) {
    KeyStateAndIndexView key_state_view =
        accessor_.FindKeyStateAndIndex(MakeKeyDescriptor(5));
    uint32_t new_count = 9000 + i;
    ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(key_state_view));
    EXPECT_TRUE(KeyMatches(key_state_view, 5));
    EXPECT_FALSE(key_state_view.version_block_);

    // The operation didn't consume any version blocks
    EXPECT_EQ(accessor_.available_data_blocks_count(), 54);

    ASSERT_TRUE(key_state_view.state_block_->has_empty_slots_thread_unsafe());
    key_state_view.state_block_->PushSubkeysCountFromWriterThread(
        VersionOffset{i}, new_count);

    // All inserted versions are visible
    for (uint32_t j = 0; j <= i; ++j) {
      EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + j), 9000 + j);
    }
    ASSERT_TRUE(accessor_.AddVersion());
  }

  // The next 4 versions will use a version block.
  // (the existing 3 versions will be copied there since they are referenced).
  for (uint32_t i = 3; i < 7; ++i) {
    KeyStateAndIndexView key_state_view =
        accessor_.FindKeyStateAndIndex(MakeKeyDescriptor(5));
    const uint32_t new_count = 9000 + i;
    ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(key_state_view));
    EXPECT_TRUE(KeyMatches(key_state_view, 5));
    ASSERT_TRUE(key_state_view.version_block_);

    // All versions are in the same version block.
    EXPECT_EQ(accessor_.available_data_blocks_count(), 53);

    ASSERT_TRUE(key_state_view.version_block_->has_empty_slots_thread_unsafe());
    key_state_view.version_block_->PushSubkeysCountFromWriterThread(
        VersionOffset{i}, new_count);

    // All inserted versions are visible
    for (uint32_t j = 0; j <= i; ++j) {
      EXPECT_EQ(GetSubkeyCountForVersion(kBaseVersion + j), 9000 + j);
    }
    EXPECT_TRUE(accessor_.AddVersion());
  }

  // Dereferencing a single version.
  header_block_->RemoveSnapshotReference(kBaseVersion + 2, *behavior_);

  KeyStateAndIndexView key_state_view =
      accessor_.FindKeyStateAndIndex(MakeKeyDescriptor(5));
  ASSERT_TRUE(accessor_.ReserveSpaceForTransaction(key_state_view));
  EXPECT_TRUE(KeyMatches(key_state_view, 5));
  ASSERT_TRUE(key_state_view.version_block_);

  // Two new blocks were allocated, since 6 out of 7 previous versions are still
  // alive and have to be preserved.
  EXPECT_EQ(accessor_.available_data_blocks_count(), 51);

  EXPECT_EQ(key_state_view.version_block_->capacity(), 15);
  EXPECT_EQ(key_state_view.version_block_->size_relaxed(), 6);
  key_state_view.version_block_->PushSubkeysCountFromWriterThread(
      VersionOffset{7}, 9007);
  EXPECT_EQ(key_state_view.version_block_->size_relaxed(), 7);

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

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
