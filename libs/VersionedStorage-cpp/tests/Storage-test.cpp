// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptorWithHandle.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyEnumerator.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Storage.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyEnumerator.h>

#include "TestBehavior.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Storage_Test : public ::testing::Test {
 protected:
  ~Storage_Test() override {
    behavior_->CheckLeakingHandles();
    EXPECT_EQ(behavior_.use_count(), 1);
  }

 protected:
  KeyDescriptorWithHandle MakeKeyDescriptor(uint64_t id) const noexcept {
    return {*behavior_, behavior_->MakeKey(id), true};
  }

  PayloadHandle MakePayload(uint64_t id) { return behavior_->MakePayload(id); }

  std::shared_ptr<TestBehavior> behavior_{std::make_shared<TestBehavior>()};
};

TEST_F(Storage_Test, initial_state_is_empty) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto snapshot = storage->GetSnapshot();
  ASSERT_TRUE(snapshot);

  EXPECT_EQ(snapshot->version(), 0);

  EXPECT_EQ(snapshot->keys_count(), 0);
  EXPECT_EQ(snapshot->subkeys_count(), 0);

  const auto key_0 = MakeKeyDescriptor(0);

  EXPECT_EQ(snapshot->GetSubkeysCount(key_0), 0);
  EXPECT_FALSE(snapshot->Get(key_0, 0).has_value());

  auto key_enumerator = snapshot->CreateKeyEnumerator();
  ASSERT_TRUE(key_enumerator);
  EXPECT_FALSE(key_enumerator->MoveNext());

  auto subkey_enumerator = snapshot->CreateSubkeyEnumerator(key_0);
  ASSERT_TRUE(subkey_enumerator);
  EXPECT_FALSE(subkey_enumerator->MoveNext());
}

TEST_F(Storage_Test, unused_transaction_cleans_after_itself) {
  auto transaction = Transaction::Create(behavior_);

  transaction->Put(MakeKeyDescriptor(5), 9000, MakePayload(13));

  transaction->Put(MakeKeyDescriptor(2), 731, MakePayload(11));
  transaction->Put(MakeKeyDescriptor(2), 731,
                   MakePayload(12));  // Overwrites the one above

  transaction->Put(MakeKeyDescriptor(3), 981, MakePayload(3));
  transaction->Delete(MakeKeyDescriptor(3), 981);  // Deletes the one above

  transaction->RequirePayload(MakeKeyDescriptor(7), 111, MakePayload(3));
  transaction->RequireMissingSubkey(MakeKeyDescriptor(7), 112);
  transaction->RequireSubkeysCount(MakeKeyDescriptor(7), 6);
}

TEST_F(Storage_Test, unsatisfied_subkeys_count_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = Transaction::Create(behavior_);

  transaction->RequireSubkeysCount(MakeKeyDescriptor(7), 6);
  EXPECT_EQ(storage->ApplyTransaction(std::move(transaction)),
            Storage::TransactionResult::
                AppliedWithNoEffectDueToUnsatisfiedPrerequisites);

  auto snapshot = storage->GetSnapshot();
  ASSERT_TRUE(snapshot);
  EXPECT_EQ(snapshot->version(), 1);
  EXPECT_EQ(snapshot->keys_count(), 0);
  EXPECT_EQ(snapshot->subkeys_count(), 0);
}

TEST_F(Storage_Test, unsatisfied_payload_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = Transaction::Create(behavior_);

  transaction->RequirePayload(MakeKeyDescriptor(7), 111, MakePayload(3));
  EXPECT_EQ(storage->ApplyTransaction(std::move(transaction)),
            Storage::TransactionResult::
                AppliedWithNoEffectDueToUnsatisfiedPrerequisites);

  auto snapshot = storage->GetSnapshot();
  ASSERT_TRUE(snapshot);
  EXPECT_EQ(snapshot->version(), 1);
  EXPECT_EQ(snapshot->keys_count(), 0);
  EXPECT_EQ(snapshot->subkeys_count(), 0);
}

TEST_F(Storage_Test, transaction_with_no_effect) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = Transaction::Create(behavior_);

  transaction->RequireMissingSubkey(MakeKeyDescriptor(7), 111);
  transaction->ClearBeforeTransaction(MakeKeyDescriptor(3));
  transaction->Delete(MakeKeyDescriptor(5), 111);

  ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
            Storage::TransactionResult::Applied);

  auto snapshot = storage->GetSnapshot();
  ASSERT_TRUE(snapshot);
  EXPECT_EQ(snapshot->version(), 1);
  EXPECT_EQ(snapshot->keys_count(), 0);
  EXPECT_EQ(snapshot->subkeys_count(), 0);
}

TEST_F(Storage_Test, simple_transactions) {
  auto storage = std::make_shared<Storage>(behavior_);

  {
    auto transaction = Transaction::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }

  auto snapshot_1 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_1);
  EXPECT_EQ(snapshot_1->version(), 1);
  EXPECT_EQ(snapshot_1->keys_count(), 1);
  EXPECT_EQ(snapshot_1->subkeys_count(), 1);

  EXPECT_EQ(snapshot_1->GetSubkeysCount(MakeKeyDescriptor(5)), 1);
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 111).has_value());
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 111), PayloadHandle{1});

  {
    auto key_enumerator = snapshot_1->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 1);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_1->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }

  // Deleting the only subkey of key 5, and adding two subkeys to key 6
  {
    auto transaction = Transaction::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(6), 222, MakePayload(2));
    transaction->Put(MakeKeyDescriptor(6), 333, MakePayload(3));
    transaction->Delete(MakeKeyDescriptor(5), 111);
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }

  auto snapshot_2 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_2);
  EXPECT_EQ(snapshot_2->version(), 2);
  EXPECT_EQ(snapshot_2->keys_count(), 1);
  EXPECT_EQ(snapshot_2->subkeys_count(), 2);

  // The subkey of key 5 was successfully deleted
  EXPECT_EQ(snapshot_2->GetSubkeysCount(MakeKeyDescriptor(5)), 0);
  EXPECT_FALSE(snapshot_2->Get(MakeKeyDescriptor(5), 111).has_value());

  // Both subkeys of the new key 6 are visible
  EXPECT_EQ(snapshot_2->GetSubkeysCount(MakeKeyDescriptor(6)), 2);
  ASSERT_TRUE(snapshot_2->Get(MakeKeyDescriptor(6), 222).has_value());
  EXPECT_EQ(snapshot_2->Get(MakeKeyDescriptor(6), 222), PayloadHandle{2});
  ASSERT_TRUE(snapshot_2->Get(MakeKeyDescriptor(6), 333).has_value());
  EXPECT_EQ(snapshot_2->Get(MakeKeyDescriptor(6), 333), PayloadHandle{3});

  {
    auto key_enumerator = snapshot_2->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{6});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 2);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{2});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_2->CreateSubkeyEnumerator(MakeKeyDescriptor(6));
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{2});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }
  // Re-checking the first snapshot (should be unaffected by the second one).
  EXPECT_EQ(snapshot_1->version(), 1);
  EXPECT_EQ(snapshot_1->keys_count(), 1);
  EXPECT_EQ(snapshot_1->subkeys_count(), 1);

  EXPECT_EQ(snapshot_1->GetSubkeysCount(MakeKeyDescriptor(5)), 1);
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 111).has_value());
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 111), PayloadHandle{1});

  {
    auto key_enumerator = snapshot_1->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 1);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_1->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }
}

TEST_F(Storage_Test, ClearBeforeTransaction) {
  auto storage = std::make_shared<Storage>(behavior_);

  {
    auto transaction = Transaction::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    transaction->Put(MakeKeyDescriptor(5), 222, MakePayload(2));
    transaction->Put(MakeKeyDescriptor(5), 333, MakePayload(3));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_1 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_1);
  {
    auto transaction = Transaction::Create(behavior_);

    // This subkey already exists (and the current payload is 2).
    transaction->Put(MakeKeyDescriptor(5), 222, MakePayload(22));

    // This subkey already exists (and currently has payload 3).
    // The transaction shouldn't change the payload, regardless of the
    // ClearBeforeTransaction() call below.
    transaction->Put(MakeKeyDescriptor(5), 333, MakePayload(3));

    // These two subkeys are new.
    transaction->Put(MakeKeyDescriptor(5), 444, MakePayload(4));
    transaction->Put(MakeKeyDescriptor(5), 555, MakePayload(5));

    // This shouldn't affect the Put operations above, but should delete the
    // subkey 111.
    transaction->ClearBeforeTransaction(MakeKeyDescriptor(5));

    transaction->RequireMissingSubkey(MakeKeyDescriptor(5), 777);

    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_2 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_2);

  // Checking both snapshots.
  EXPECT_EQ(snapshot_1->version(), 1);
  EXPECT_EQ(snapshot_1->keys_count(), 1);
  EXPECT_EQ(snapshot_1->subkeys_count(), 3);

  EXPECT_EQ(snapshot_1->GetSubkeysCount(MakeKeyDescriptor(5)), 3);
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 111).has_value());
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 222).has_value());
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 333).has_value());
  EXPECT_FALSE(snapshot_1->Get(MakeKeyDescriptor(5), 444).has_value());
  EXPECT_FALSE(snapshot_1->Get(MakeKeyDescriptor(5), 555).has_value());
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 111), PayloadHandle{1});
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 222), PayloadHandle{2});
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 333), PayloadHandle{3});

  {
    auto key_enumerator = snapshot_1->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 3);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{2});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_1->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{2});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }

  EXPECT_EQ(snapshot_2->version(), 2);
  EXPECT_EQ(snapshot_2->keys_count(), 1);
  EXPECT_EQ(snapshot_2->subkeys_count(), 4);

  EXPECT_EQ(snapshot_2->GetSubkeysCount(MakeKeyDescriptor(5)), 4);

  // This one was implicitly deleted due to the ClearBeforeTransaction() call.
  EXPECT_FALSE(snapshot_2->Get(MakeKeyDescriptor(5), 111).has_value());
  ASSERT_TRUE(snapshot_2->Get(MakeKeyDescriptor(5), 222).has_value());
  ASSERT_TRUE(snapshot_2->Get(MakeKeyDescriptor(5), 333).has_value());
  ASSERT_TRUE(snapshot_2->Get(MakeKeyDescriptor(5), 444).has_value());
  ASSERT_TRUE(snapshot_2->Get(MakeKeyDescriptor(5), 555).has_value());
  EXPECT_EQ(snapshot_2->Get(MakeKeyDescriptor(5), 222), PayloadHandle{22});
  // The transaction tried to overwrite the subkey with the same value here,
  // so it should stay unchanged (and not touched by ClearBeforeTransaction()).
  EXPECT_EQ(snapshot_2->Get(MakeKeyDescriptor(5), 333), PayloadHandle{3});
  EXPECT_EQ(snapshot_2->Get(MakeKeyDescriptor(5), 444), PayloadHandle{4});
  EXPECT_EQ(snapshot_2->Get(MakeKeyDescriptor(5), 555), PayloadHandle{5});

  {
    auto key_enumerator = snapshot_2->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 4);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{22});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 444);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{4});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 555);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{5});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_2->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{22});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 444);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{4});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 555);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{5});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }
}

TEST_F(Storage_Test, ClearBeforeTransaction_entire_key) {
  auto storage = std::make_shared<Storage>(behavior_);

  {
    auto transaction = Transaction::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    transaction->Put(MakeKeyDescriptor(5), 222, MakePayload(2));
    transaction->Put(MakeKeyDescriptor(5), 333, MakePayload(3));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_1 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_1);
  {
    auto transaction = Transaction::Create(behavior_);
    transaction->ClearBeforeTransaction(MakeKeyDescriptor(5));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_2 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_2);

  // Checking both snapshots.
  EXPECT_EQ(snapshot_1->version(), 1);
  EXPECT_EQ(snapshot_1->keys_count(), 1);
  EXPECT_EQ(snapshot_1->subkeys_count(), 3);

  EXPECT_EQ(snapshot_1->GetSubkeysCount(MakeKeyDescriptor(5)), 3);
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 111).has_value());
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 222).has_value());
  ASSERT_TRUE(snapshot_1->Get(MakeKeyDescriptor(5), 333).has_value());
  EXPECT_FALSE(snapshot_1->Get(MakeKeyDescriptor(5), 444).has_value());
  EXPECT_FALSE(snapshot_1->Get(MakeKeyDescriptor(5), 555).has_value());
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 111), PayloadHandle{1});
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 222), PayloadHandle{2});
  EXPECT_EQ(snapshot_1->Get(MakeKeyDescriptor(5), 333), PayloadHandle{3});

  {
    auto key_enumerator = snapshot_1->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 3);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{2});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_1->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 111);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{1});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 222);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{2});
    ASSERT_TRUE(subkey_enumerator->MoveNext());
    EXPECT_EQ(subkey_enumerator->current_subkey(), 333);
    EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }

  EXPECT_EQ(snapshot_2->version(), 2);
  EXPECT_EQ(snapshot_2->keys_count(), 0);
  EXPECT_EQ(snapshot_2->subkeys_count(), 0);
  EXPECT_EQ(snapshot_2->GetSubkeysCount(MakeKeyDescriptor(5)), 0);

  // This one was implicitly deleted due to the ClearBeforeTransaction() call.
  EXPECT_FALSE(snapshot_2->Get(MakeKeyDescriptor(5), 111).has_value());
  EXPECT_FALSE(snapshot_2->Get(MakeKeyDescriptor(5), 222).has_value());
  EXPECT_FALSE(snapshot_2->Get(MakeKeyDescriptor(5), 333).has_value());
  {
    auto key_enumerator = snapshot_2->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot_2->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }
}

TEST_F(Storage_Test, simple_blob_reallocation) {
  auto storage = std::make_shared<Storage>(behavior_);

  {
    auto transaction = Transaction::Create(behavior_);

    EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

    // The index is not large enough to hold all blocks.
    // This will trigger a reallocation.
    for (uint64_t i = 0; i < 7; ++i) {
      transaction->Put(MakeKeyDescriptor(5), 100u + i, MakePayload(i));
    }
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);

    // Allocates a new one-page-large blob with a larger index (but the number
    // of blocks is still small enough to reserve only one page).
    EXPECT_EQ(behavior_->total_allocated_pages_count(), 2);
  }

  auto snapshot = storage->GetSnapshot();
  ASSERT_TRUE(snapshot);
  EXPECT_EQ(snapshot->version(), 1);
  EXPECT_EQ(snapshot->keys_count(), 1);
  EXPECT_EQ(snapshot->subkeys_count(), 7);
  EXPECT_EQ(snapshot->GetSubkeysCount(MakeKeyDescriptor(5)), 7);
  for (uint32_t i = 0; i < 7; ++i) {
    ASSERT_TRUE(snapshot->Get(MakeKeyDescriptor(5), 100u + i).has_value());
    EXPECT_EQ(snapshot->Get(MakeKeyDescriptor(5), 100u + i), PayloadHandle{i});
  }

  {
    auto key_enumerator = snapshot->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 7);
    auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
    ASSERT_TRUE(subkey_enumerator);
    for (uint32_t i = 0; i < 7; ++i) {
      ASSERT_TRUE(subkey_enumerator->MoveNext());
      EXPECT_EQ(subkey_enumerator->current_subkey(), 100u + i);
      EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{i});
    }
    EXPECT_FALSE(subkey_enumerator->MoveNext());
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
  {
    auto subkey_enumerator =
        snapshot->CreateSubkeyEnumerator(MakeKeyDescriptor(5));
    ASSERT_TRUE(subkey_enumerator);
    for (uint32_t i = 0; i < 7; ++i) {
      ASSERT_TRUE(subkey_enumerator->MoveNext());
      EXPECT_EQ(subkey_enumerator->current_subkey(), 100u + i);
      EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{i});
    }
    EXPECT_FALSE(subkey_enumerator->MoveNext());
  }
}

TEST_F(Storage_Test, single_subkey_versions_reallocation) {
  auto storage = std::make_shared<Storage>(behavior_);

  std::vector<std::shared_ptr<Snapshot>> snapshots;

  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  // The storage will be reallocated multiple times
  for (size_t i = 0; i < 1'000; ++i) {
    auto transaction = Transaction::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 42, MakePayload(i % 10));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
    snapshots.emplace_back(storage->GetSnapshot());
  }
  // Reallocated multiple times
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 13);

  const auto key_5 = MakeKeyDescriptor(5);
  for (size_t i = 0; i < snapshots.size(); ++i) {
    auto& snapshot = snapshots[i];
    ASSERT_TRUE(snapshot);
    EXPECT_EQ(snapshot->version(), i + 1);
    EXPECT_EQ(snapshot->keys_count(), 1);
    EXPECT_EQ(snapshot->subkeys_count(), 1);
    EXPECT_EQ(snapshot->GetSubkeysCount(key_5), 1);
    EXPECT_EQ(snapshot->Get(key_5, 42), PayloadHandle{i % 10});
  }
}

TEST_F(Storage_Test, reallocated_with_cleanups) {
  auto storage = std::make_shared<Storage>(behavior_);

  std::vector<std::shared_ptr<Snapshot>> snapshots;

  {
    auto transaction = Transaction::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 100, MakePayload(1));
    transaction->Put(MakeKeyDescriptor(5), 200, MakePayload(2));

    transaction->Put(MakeKeyDescriptor(6), 100, MakePayload(10));
    transaction->Put(MakeKeyDescriptor(6), 200, MakePayload(20));
    transaction->Put(MakeKeyDescriptor(6), 300, MakePayload(30));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }
  // Still fits into one page
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  auto snapshot_1 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_1);
  {
    auto transaction = Transaction::Create(behavior_);

    transaction->ClearBeforeTransaction(MakeKeyDescriptor(5));
    transaction->ClearBeforeTransaction(MakeKeyDescriptor(6));

    // New subkey
    transaction->Put(MakeKeyDescriptor(5), 300, MakePayload(3));

    // Same as before (should prevent the cleanup)
    transaction->Put(MakeKeyDescriptor(6), 200, MakePayload(20));
    ASSERT_EQ(storage->ApplyTransaction(std::move(transaction)),
              Storage::TransactionResult::Applied);
  }

  // Had to reallocate the blob with a larger index (but the number of blocks is
  // still small enough to fit into one page).
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 2);

  auto snapshot_2 = storage->GetSnapshot();
  ASSERT_TRUE(snapshot_2);

  const auto key_5 = MakeKeyDescriptor(5);
  const auto key_6 = MakeKeyDescriptor(6);

  // Checking the old snapshot
  EXPECT_EQ(snapshot_1->version(), 1);
  EXPECT_EQ(snapshot_1->keys_count(), 2);
  EXPECT_EQ(snapshot_1->subkeys_count(), 5);
  EXPECT_EQ(snapshot_1->GetSubkeysCount(key_5), 2);
  EXPECT_EQ(snapshot_1->GetSubkeysCount(key_6), 3);
  EXPECT_EQ(snapshot_1->Get(key_5, 100), PayloadHandle{1});
  EXPECT_EQ(snapshot_1->Get(key_5, 200), PayloadHandle{2});
  EXPECT_EQ(snapshot_1->Get(key_6, 100), PayloadHandle{10});
  EXPECT_EQ(snapshot_1->Get(key_6, 200), PayloadHandle{20});
  EXPECT_EQ(snapshot_1->Get(key_6, 300), PayloadHandle{30});

  // Checking the new snapshot
  EXPECT_EQ(snapshot_2->version(), 2);
  EXPECT_EQ(snapshot_2->keys_count(), 2);
  EXPECT_EQ(snapshot_2->subkeys_count(), 2);
  EXPECT_EQ(snapshot_2->GetSubkeysCount(key_5), 1);
  EXPECT_EQ(snapshot_2->GetSubkeysCount(key_6), 1);
  EXPECT_EQ(snapshot_2->Get(key_5, 300), PayloadHandle{3});
  EXPECT_EQ(snapshot_2->Get(key_6, 200), PayloadHandle{20});
  EXPECT_FALSE(snapshot_2->Get(key_5, 100).has_value());
  EXPECT_FALSE(snapshot_2->Get(key_5, 200).has_value());
  EXPECT_FALSE(snapshot_2->Get(key_6, 100).has_value());

  {
    auto key_enumerator = snapshot_2->CreateKeyEnumerator();
    ASSERT_TRUE(key_enumerator);
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{5});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 1);
    {
      auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
      ASSERT_TRUE(subkey_enumerator);
      ASSERT_TRUE(subkey_enumerator->MoveNext());
      EXPECT_EQ(subkey_enumerator->current_subkey(), 300);
      EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{3});
      EXPECT_FALSE(subkey_enumerator->MoveNext());
    }
    ASSERT_TRUE(key_enumerator->MoveNext());
    EXPECT_EQ(key_enumerator->current_key(), KeyHandle{6});
    EXPECT_EQ(key_enumerator->current_subkeys_count(), 1);
    {
      auto subkey_enumerator = key_enumerator->CreateSubkeyEnumerator();
      ASSERT_TRUE(subkey_enumerator);
      ASSERT_TRUE(subkey_enumerator->MoveNext());
      EXPECT_EQ(subkey_enumerator->current_subkey(), 200);
      EXPECT_EQ(subkey_enumerator->current_payload_handle(), PayloadHandle{20});
      EXPECT_FALSE(subkey_enumerator->MoveNext());
    }
    EXPECT_FALSE(key_enumerator->MoveNext());
  }
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
