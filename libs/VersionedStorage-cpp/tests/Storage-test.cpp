// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamWriter.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptorWithHandle.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Storage.h>

#include "TestBehavior.h"

#include <array>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Storage_Test : public ::testing::Test {
 protected:
  ~Storage_Test() override { ResetBehavior(); }

 protected:
  KeyDescriptorWithHandle MakeKeyDescriptor(uint64_t id) const noexcept {
    return {*behavior_, behavior_->MakeKey(id), true};
  }

  PayloadHandle MakePayload(uint64_t id) { return behavior_->MakePayload(id); }

  std::shared_ptr<TestBehavior> behavior_{std::make_shared<TestBehavior>()};

  std::vector<char> SerializeTransaction(TransactionBuilder& transaction) {
    Serialization::BitstreamWriter bitstream_writer;
    std::vector<std::byte> byte_stream;
    transaction.Serialize(bitstream_writer, byte_stream);
    auto bitstream_bytes = bitstream_writer.Finalize();
    std::vector<char> buf(bitstream_bytes.size() + byte_stream.size());
    char* dst = buf.data();
    memcpy(dst, bitstream_bytes.data(), bitstream_bytes.size());
    memcpy(dst + bitstream_bytes.size(), byte_stream.data(),
           byte_stream.size());
    return buf;
  }

  Storage::TransactionResult ApplyTransaction(
      Storage& storage,
      TransactionBuilder& transaction_builder) {
    Serialization::BitstreamWriter bitstream_writer;
    std::vector<std::byte> byte_stream;
    transaction_builder.Serialize(bitstream_writer, byte_stream);
    auto bitstream_bytes = bitstream_writer.Finalize();
    std::vector<char> buf(bitstream_bytes.size() + byte_stream.size());
    char* dst = buf.data();
    memcpy(dst, bitstream_bytes.data(), bitstream_bytes.size());
    memcpy(dst + bitstream_bytes.size(), byte_stream.data(),
           byte_stream.size());
    return storage.ApplyTransaction({buf.data(), buf.size()});
  }

  void ResetBehavior() {
    behavior_->CheckLeakingHandles();
    EXPECT_EQ(behavior_.use_count(), 1);
    behavior_ = std::make_shared<TestBehavior>();
  }
};

TEST_F(Storage_Test, initial_state_is_empty) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto snapshot = storage->GetSnapshot();

  EXPECT_EQ(snapshot.version(), 0);

  EXPECT_EQ(snapshot.keys_count(), 0);
  EXPECT_EQ(snapshot.subkeys_count(), 0);

  const auto key_0 = MakeKeyDescriptor(0);

  EXPECT_EQ(snapshot.GetSubkeysCount(key_0), 0);
  EXPECT_FALSE(snapshot.Get(key_0, 0));

  EXPECT_EQ(snapshot.begin(), snapshot.end());
  EXPECT_FALSE(snapshot.Get(key_0));
}

TEST_F(Storage_Test, unused_transaction_builder_cleans_after_itself) {
  auto transaction = TransactionBuilder::Create(behavior_);

  transaction->Put(MakeKeyDescriptor(5), 9000, MakePayload(13));

  transaction->Put(MakeKeyDescriptor(2), 731, MakePayload(11));
  transaction->Put(MakeKeyDescriptor(2), 731,
                   MakePayload(12));  // Overwrites the one above

  transaction->Put(MakeKeyDescriptor(3), 981, MakePayload(3));
  transaction->Delete(MakeKeyDescriptor(3), 981);  // Deletes the one above

  transaction->RequireExactPayload(MakeKeyDescriptor(7), 111, MakePayload(3));
  transaction->RequireMissingSubkey(MakeKeyDescriptor(7), 112);
  transaction->RequireSubkeysCount(MakeKeyDescriptor(7), 6);
}

TEST_F(Storage_Test, unsatisfied_subkeys_count_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = TransactionBuilder::Create(behavior_);
  transaction->RequireSubkeysCount(MakeKeyDescriptor(7), 6);
  EXPECT_EQ(ApplyTransaction(*storage, *transaction),
            Storage::TransactionResult::
                AppliedWithNoEffectDueToUnsatisfiedPrerequisites);
  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 1);
  EXPECT_EQ(snapshot.keys_count(), 0);
  EXPECT_EQ(snapshot.subkeys_count(), 0);
}

TEST_F(Storage_Test, unsatisfied_payload_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = TransactionBuilder::Create(behavior_);
  transaction->RequireExactPayload(MakeKeyDescriptor(7), 111, MakePayload(3));
  EXPECT_EQ(ApplyTransaction(*storage, *transaction),
            Storage::TransactionResult::
                AppliedWithNoEffectDueToUnsatisfiedPrerequisites);

  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 1);
  EXPECT_EQ(snapshot.keys_count(), 0);
  EXPECT_EQ(snapshot.subkeys_count(), 0);
}

TEST_F(Storage_Test, unsatisfied_present_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = TransactionBuilder::Create(behavior_);
  transaction->RequirePresentSubkey(MakeKeyDescriptor(7), 111);
  EXPECT_EQ(ApplyTransaction(*storage, *transaction),
            Storage::TransactionResult::
                AppliedWithNoEffectDueToUnsatisfiedPrerequisites);

  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 1);
  EXPECT_EQ(snapshot.keys_count(), 0);
  EXPECT_EQ(snapshot.subkeys_count(), 0);
}

TEST_F(Storage_Test, transaction_with_no_effect) {
  auto storage = std::make_shared<Storage>(behavior_);
  auto transaction = TransactionBuilder::Create(behavior_);
  transaction->RequireMissingSubkey(MakeKeyDescriptor(7), 111);
  transaction->ClearBeforeTransaction(MakeKeyDescriptor(3));
  transaction->Delete(MakeKeyDescriptor(5), 111);
  ASSERT_EQ(ApplyTransaction(*storage, *transaction),
            Storage::TransactionResult::Applied);

  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 1);
  EXPECT_EQ(snapshot.keys_count(), 0);
  EXPECT_EQ(snapshot.subkeys_count(), 0);
}

TEST_F(Storage_Test, simple_transactions) {
  auto storage = std::make_shared<Storage>(behavior_);
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }

  auto snapshot_1 = storage->GetSnapshot();
  EXPECT_EQ(snapshot_1.version(), 1);
  EXPECT_EQ(snapshot_1.keys_count(), 1);
  EXPECT_EQ(snapshot_1.subkeys_count(), 1);

  EXPECT_EQ(snapshot_1.GetSubkeysCount(MakeKeyDescriptor(5)), 1);
  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 111));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).payload(),
            PayloadHandle{1});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).version(), 1);

  {
    KeyIterator key_it = snapshot_1.begin();
    ASSERT_NE(key_it, snapshot_1.end());

    ASSERT_EQ(key_it->key_handle(), KeyHandle{5});
    ASSERT_EQ(key_it->subkeys_count(), 1);

    auto subkeys = snapshot_1.GetSubkeys(*key_it);
    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  {
    std::optional<KeyView> key_view = snapshot_1.Get(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_view);
    ASSERT_EQ(key_view->subkeys_count(), 1);

    auto subkeys = snapshot_1.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }

  // Deleting the only subkey of key 5, and adding two subkeys to key 6
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(6), 222, MakePayload(2));
    transaction->Put(MakeKeyDescriptor(6), 333, MakePayload(3));
    transaction->Delete(MakeKeyDescriptor(5), 111);

    // Adding a few satisfied prerequisites
    transaction->RequireExactVersion(MakeKeyDescriptor(5), 111, 1);
    transaction->RequireMissingSubkey(MakeKeyDescriptor(6), 222);
    transaction->RequireMissingSubkey(MakeKeyDescriptor(6), 999);
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }

  auto snapshot_2 = storage->GetSnapshot();
  EXPECT_EQ(snapshot_2.version(), 2);
  EXPECT_EQ(snapshot_2.keys_count(), 1);
  EXPECT_EQ(snapshot_2.subkeys_count(), 2);

  // The subkey of key 5 was successfully deleted
  EXPECT_EQ(snapshot_2.GetSubkeysCount(MakeKeyDescriptor(5)), 0);
  EXPECT_FALSE(snapshot_2.Get(MakeKeyDescriptor(5), 111));

  // Both subkeys of the new key 6 are visible
  EXPECT_EQ(snapshot_2.GetSubkeysCount(MakeKeyDescriptor(6)), 2);
  ASSERT_TRUE(snapshot_2.Get(MakeKeyDescriptor(6), 222));
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(6), 222).payload(),
            PayloadHandle{2});
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(6), 222).version(), 2);

  ASSERT_TRUE(snapshot_2.Get(MakeKeyDescriptor(6), 333));
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(6), 333).payload(),
            PayloadHandle{3});
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(6), 333).version(), 2);

  {
    KeyIterator key_it = snapshot_2.begin();
    ASSERT_NE(key_it, snapshot_2.end());

    ASSERT_EQ(key_it->key_handle(), KeyHandle{6});
    ASSERT_EQ(key_it->subkeys_count(), 2);

    auto subkeys = snapshot_2.GetSubkeys(*key_it);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{2});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  {
    std::optional<KeyView> key_view = snapshot_2.Get(MakeKeyDescriptor(5));
    ASSERT_FALSE(key_view);
    key_view = snapshot_2.Get(MakeKeyDescriptor(6));
    ASSERT_TRUE(key_view);
    ASSERT_EQ(key_view->subkeys_count(), 2);

    auto subkeys = snapshot_2.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{2});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  // Re-checking the first snapshot (should be unaffected by the second one).
  EXPECT_EQ(snapshot_1.version(), 1);
  EXPECT_EQ(snapshot_1.keys_count(), 1);
  EXPECT_EQ(snapshot_1.subkeys_count(), 1);

  EXPECT_EQ(snapshot_1.GetSubkeysCount(MakeKeyDescriptor(5)), 1);
  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 111));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).payload(),
            PayloadHandle{1});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).version(), 1);

  {
    KeyIterator key_it = snapshot_1.begin();
    ASSERT_NE(key_it, snapshot_1.end());

    ASSERT_EQ(key_it->key_handle(), KeyHandle{5});
    ASSERT_EQ(key_it->subkeys_count(), 1);

    auto subkeys = snapshot_1.GetSubkeys(*key_it);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  {
    std::optional<KeyView> key_view = snapshot_1.Get(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_view);
    ASSERT_EQ(key_view->subkeys_count(), 1);

    auto subkeys = snapshot_1.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
}

TEST_F(Storage_Test, unsatisfied_missing_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  auto transaction = TransactionBuilder::Create(behavior_);
  transaction->RequireMissingSubkey(MakeKeyDescriptor(5), 111);
  EXPECT_EQ(ApplyTransaction(*storage, *transaction),
            Storage::TransactionResult::
                AppliedWithNoEffectDueToUnsatisfiedPrerequisites);

  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 2);
  EXPECT_EQ(snapshot.keys_count(), 1);
  EXPECT_EQ(snapshot.subkeys_count(), 1);
}

TEST_F(Storage_Test, unsatisfied_version_prerequisite) {
  auto storage = std::make_shared<Storage>(behavior_);
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->RequireExactVersion(MakeKeyDescriptor(5), 111, 2);
    EXPECT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::
                  AppliedWithNoEffectDueToUnsatisfiedPrerequisites);
  }
  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 2);
  EXPECT_EQ(snapshot.keys_count(), 1);
  EXPECT_EQ(snapshot.subkeys_count(), 1);
}

TEST_F(Storage_Test, ClearBeforeTransaction) {
  auto storage{std::make_shared<Storage>(behavior_)};
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    transaction->Put(MakeKeyDescriptor(5), 222, MakePayload(2));
    transaction->Put(MakeKeyDescriptor(5), 333, MakePayload(3));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_1 = storage->GetSnapshot();
  {
    auto transaction = TransactionBuilder::Create(behavior_);

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

    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_2 = storage->GetSnapshot();

  // Checking both snapshots.
  EXPECT_EQ(snapshot_1.version(), 1);
  EXPECT_EQ(snapshot_1.keys_count(), 1);
  EXPECT_EQ(snapshot_1.subkeys_count(), 3);

  EXPECT_EQ(snapshot_1.GetSubkeysCount(MakeKeyDescriptor(5)), 3);

  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 111));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).payload(),
            PayloadHandle{1});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 222));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 222).payload(),
            PayloadHandle{2});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 222).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 333));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 333).payload(),
            PayloadHandle{3});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 333).version(), 1);

  EXPECT_FALSE(snapshot_1.Get(MakeKeyDescriptor(5), 444));
  EXPECT_FALSE(snapshot_1.Get(MakeKeyDescriptor(5), 555));

  {
    KeyIterator key_it = snapshot_1.begin();
    ASSERT_NE(key_it, snapshot_1.end());

    ASSERT_EQ(key_it->key_handle(), KeyHandle{5});
    ASSERT_EQ(key_it->subkeys_count(), 3);

    auto subkeys = snapshot_1.GetSubkeys(*key_it);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{2});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  {
    std::optional<KeyView> key_view = snapshot_1.Get(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_view);
    ASSERT_EQ(key_view->subkeys_count(), 3);

    auto subkeys = snapshot_1.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{2});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }

  EXPECT_EQ(snapshot_2.version(), 2);
  EXPECT_EQ(snapshot_2.keys_count(), 1);
  EXPECT_EQ(snapshot_2.subkeys_count(), 4);

  EXPECT_EQ(snapshot_2.GetSubkeysCount(MakeKeyDescriptor(5)), 4);

  // This one was implicitly deleted due to the ClearBeforeTransaction() call.
  EXPECT_FALSE(snapshot_2.Get(MakeKeyDescriptor(5), 111));
  ASSERT_TRUE(snapshot_2.Get(MakeKeyDescriptor(5), 222));
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 222).payload(),
            PayloadHandle{22});
  // Updated recently
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 222).version(), 2);

  ASSERT_TRUE(snapshot_2.Get(MakeKeyDescriptor(5), 333));
  // The transaction tried to overwrite the subkey with the same value here,
  // so it should stay unchanged (and not touched by
  // ClearBeforeTransaction()).
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 333).payload(),
            PayloadHandle{3});
  // The version stays at 1 (the new payload was identical to old one, so we
  // didn't change anything).
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 333).version(), 1);

  ASSERT_TRUE(snapshot_2.Get(MakeKeyDescriptor(5), 444));
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 444).payload(),
            PayloadHandle{4});
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 444).version(), 2);

  ASSERT_TRUE(snapshot_2.Get(MakeKeyDescriptor(5), 555));
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 555).payload(),
            PayloadHandle{5});
  EXPECT_EQ(snapshot_2.Get(MakeKeyDescriptor(5), 555).version(), 2);

  {
    KeyIterator key_it = snapshot_2.begin();
    ASSERT_NE(key_it, snapshot_2.end());

    ASSERT_EQ(key_it->key_handle(), KeyHandle{5});
    ASSERT_EQ(key_it->subkeys_count(), 4);

    auto subkeys = snapshot_2.GetSubkeys(*key_it);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{22});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 444);
    EXPECT_EQ(it->payload(), PayloadHandle{4});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 555);
    EXPECT_EQ(it->payload(), PayloadHandle{5});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  {
    std::optional<KeyView> key_view = snapshot_2.Get(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_view);
    ASSERT_EQ(key_view->subkeys_count(), 4);

    auto subkeys = snapshot_2.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{22});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 444);
    EXPECT_EQ(it->payload(), PayloadHandle{4});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 555);
    EXPECT_EQ(it->payload(), PayloadHandle{5});
    EXPECT_EQ(it->version(), 2);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
}

TEST_F(Storage_Test, ClearBeforeTransaction_entire_key) {
  auto storage{std::make_shared<Storage>(behavior_)};
  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 111, MakePayload(1));
    transaction->Put(MakeKeyDescriptor(5), 222, MakePayload(2));
    transaction->Put(MakeKeyDescriptor(5), 333, MakePayload(3));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_1 = storage->GetSnapshot();

  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->ClearBeforeTransaction(MakeKeyDescriptor(5));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  auto snapshot_2 = storage->GetSnapshot();

  // Checking both snapshots.
  EXPECT_EQ(snapshot_1.version(), 1);
  EXPECT_EQ(snapshot_1.keys_count(), 1);
  EXPECT_EQ(snapshot_1.subkeys_count(), 3);

  EXPECT_EQ(snapshot_1.GetSubkeysCount(MakeKeyDescriptor(5)), 3);

  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 111));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).payload(),
            PayloadHandle{1});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 111).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 222));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 222).payload(),
            PayloadHandle{2});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 222).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(MakeKeyDescriptor(5), 333));
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 333).payload(),
            PayloadHandle{3});
  EXPECT_EQ(snapshot_1.Get(MakeKeyDescriptor(5), 333).version(), 1);

  EXPECT_FALSE(snapshot_1.Get(MakeKeyDescriptor(5), 444));
  EXPECT_FALSE(snapshot_1.Get(MakeKeyDescriptor(5), 555));

  {
    KeyIterator key_it = snapshot_1.begin();
    ASSERT_NE(key_it, snapshot_1.end());

    ASSERT_EQ(key_it->key_handle(), KeyHandle{5});
    ASSERT_EQ(key_it->subkeys_count(), 3);

    auto subkeys = snapshot_1.GetSubkeys(*key_it);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{2});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }
  {
    std::optional<KeyView> key_view = snapshot_1.Get(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_view);
    ASSERT_EQ(key_view->subkeys_count(), 3);

    auto subkeys = snapshot_1.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 111);
    EXPECT_EQ(it->payload(), PayloadHandle{1});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 222);
    EXPECT_EQ(it->payload(), PayloadHandle{2});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_NE(it, subkeys.end());
    EXPECT_EQ(it->subkey(), 333);
    EXPECT_EQ(it->payload(), PayloadHandle{3});
    EXPECT_EQ(it->version(), 1);
    ++it;
    ASSERT_EQ(it, subkeys.end());
  }

  EXPECT_EQ(snapshot_2.version(), 2);
  EXPECT_EQ(snapshot_2.keys_count(), 0);
  EXPECT_EQ(snapshot_2.subkeys_count(), 0);
  EXPECT_EQ(snapshot_2.GetSubkeysCount(MakeKeyDescriptor(5)), 0);

  // This one was implicitly deleted due to the ClearBeforeTransaction() call.
  EXPECT_FALSE(snapshot_2.Get(MakeKeyDescriptor(5), 111));
  EXPECT_FALSE(snapshot_2.Get(MakeKeyDescriptor(5), 222));
  EXPECT_FALSE(snapshot_2.Get(MakeKeyDescriptor(5), 333));
  ASSERT_EQ(snapshot_2.begin(), snapshot_2.end());
  ASSERT_FALSE(snapshot_2.Get(MakeKeyDescriptor(5)));
}

TEST_F(Storage_Test, simple_blob_reallocation) {
  ResetBehavior();
  auto storage{std::make_shared<Storage>(behavior_)};
  {
    auto transaction = TransactionBuilder::Create(behavior_);

    EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

    // The index is not large enough to hold all blocks.
    // This will trigger a reallocation.
    for (uint64_t i = 0; i < 7; ++i) {
      transaction->Put(MakeKeyDescriptor(5), 100u + i, MakePayload(i));
    }
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);

    // Allocates a new one-page-large blob with a larger index (but the number
    // of blocks is still small enough to reserve only one page).
    EXPECT_EQ(behavior_->total_allocated_pages_count(), 2);
  }

  auto snapshot = storage->GetSnapshot();
  EXPECT_EQ(snapshot.version(), 1);
  EXPECT_EQ(snapshot.keys_count(), 1);
  EXPECT_EQ(snapshot.subkeys_count(), 7);
  EXPECT_EQ(snapshot.GetSubkeysCount(MakeKeyDescriptor(5)), 7);
  for (uint32_t i = 0; i < 7; ++i) {
    ASSERT_TRUE(snapshot.Get(MakeKeyDescriptor(5), 100u + i));
    EXPECT_EQ(snapshot.Get(MakeKeyDescriptor(5), 100u + i).payload(),
              PayloadHandle{i});
    EXPECT_EQ(snapshot.Get(MakeKeyDescriptor(5), 100u + i).version(), 1);
  }

  {
    KeyIterator key_it = snapshot.begin();
    ASSERT_NE(key_it, snapshot.end());

    EXPECT_EQ(key_it->key_handle(), KeyHandle{5});
    EXPECT_EQ(key_it->subkeys_count(), 7);

    auto subkeys = snapshot.GetSubkeys(*key_it);

    auto it = subkeys.begin();
    for (uint32_t i = 0; i < 7; ++i) {
      ASSERT_NE(it, subkeys.end());
      EXPECT_EQ(it->subkey(), 100u + i);
      EXPECT_EQ(it->payload(), PayloadHandle{i});
      ++it;
    }
    ASSERT_EQ(it, subkeys.end());
    ++key_it;
    ASSERT_EQ(key_it, snapshot.end());
  }
  {
    std::optional<KeyView> key_view = snapshot.Get(MakeKeyDescriptor(5));
    ASSERT_TRUE(key_view);

    auto subkeys = snapshot.GetSubkeys(*key_view);

    SubkeyIterator it = subkeys.begin();

    for (uint32_t i = 0; i < 7; ++i) {
      ASSERT_NE(it, subkeys.end());
      EXPECT_EQ(it->subkey(), 100u + i);
      EXPECT_EQ(it->payload(), PayloadHandle{i});
      EXPECT_EQ(it->version(), 1);
      ++it;
    }
    ASSERT_EQ(it, subkeys.end());
  }
}

TEST_F(Storage_Test, single_subkey_versions_reallocation) {
  ResetBehavior();
  auto storage{std::make_shared<Storage>(behavior_)};
  std::vector<Snapshot> snapshots;

  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);

  // The storage will be reallocated multiple times
  for (size_t i = 0; i < 1'000; ++i) {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 42, MakePayload(i % 10));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
    snapshots.emplace_back(storage->GetSnapshot());
  }
  // Reallocated multiple times
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 13);

  const auto key_5 = MakeKeyDescriptor(5);
  for (size_t i = 0; i < snapshots.size(); ++i) {
    auto& snapshot = snapshots[i];
    EXPECT_EQ(snapshot.version(), i + 1);
    EXPECT_EQ(snapshot.keys_count(), 1);
    EXPECT_EQ(snapshot.subkeys_count(), 1);
    EXPECT_EQ(snapshot.GetSubkeysCount(key_5), 1);
    ASSERT_TRUE(snapshot.Get(key_5, 42));
    EXPECT_EQ(snapshot.Get(key_5, 42).payload(), PayloadHandle{i % 10});
    EXPECT_EQ(snapshot.Get(key_5, 42).version(), i + 1);
  }
}

TEST_F(Storage_Test, reallocated_with_cleanups) {
  ResetBehavior();
  auto storage{std::make_shared<Storage>(behavior_)};
  std::vector<std::shared_ptr<Snapshot>> snapshots;

  {
    auto transaction = TransactionBuilder::Create(behavior_);
    transaction->Put(MakeKeyDescriptor(5), 100, MakePayload(1));
    transaction->Put(MakeKeyDescriptor(5), 200, MakePayload(2));

    transaction->Put(MakeKeyDescriptor(6), 100, MakePayload(10));
    transaction->Put(MakeKeyDescriptor(6), 200, MakePayload(20));
    transaction->Put(MakeKeyDescriptor(6), 300, MakePayload(30));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }
  // Still fits into one page
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 1);
  auto snapshot_1 = storage->GetSnapshot();
  {
    auto transaction = TransactionBuilder::Create(behavior_);

    transaction->ClearBeforeTransaction(MakeKeyDescriptor(5));
    transaction->ClearBeforeTransaction(MakeKeyDescriptor(6));

    // New subkey
    transaction->Put(MakeKeyDescriptor(5), 300, MakePayload(3));

    // Same as before (should prevent the cleanup)
    transaction->Put(MakeKeyDescriptor(6), 200, MakePayload(20));
    ASSERT_EQ(ApplyTransaction(*storage, *transaction),
              Storage::TransactionResult::Applied);
  }

  // Had to reallocate the blob with a larger index (but the number of blocks
  // is still small enough to fit into one page).
  EXPECT_EQ(behavior_->total_allocated_pages_count(), 2);

  auto snapshot_2 = storage->GetSnapshot();

  const auto key_5 = MakeKeyDescriptor(5);
  const auto key_6 = MakeKeyDescriptor(6);

  // Checking the old snapshot
  EXPECT_EQ(snapshot_1.version(), 1);
  EXPECT_EQ(snapshot_1.keys_count(), 2);
  EXPECT_EQ(snapshot_1.subkeys_count(), 5);
  EXPECT_EQ(snapshot_1.GetSubkeysCount(key_5), 2);
  EXPECT_EQ(snapshot_1.GetSubkeysCount(key_6), 3);
  ASSERT_TRUE(snapshot_1.Get(key_5, 100));
  EXPECT_EQ(snapshot_1.Get(key_5, 100).payload(), PayloadHandle{1});
  EXPECT_EQ(snapshot_1.Get(key_5, 100).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(key_5, 200));
  EXPECT_EQ(snapshot_1.Get(key_5, 200).payload(), PayloadHandle{2});
  EXPECT_EQ(snapshot_1.Get(key_5, 200).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(key_6, 100));
  EXPECT_EQ(snapshot_1.Get(key_6, 100).payload(), PayloadHandle{10});
  EXPECT_EQ(snapshot_1.Get(key_6, 100).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(key_6, 200));
  EXPECT_EQ(snapshot_1.Get(key_6, 200).payload(), PayloadHandle{20});
  EXPECT_EQ(snapshot_1.Get(key_6, 200).version(), 1);

  ASSERT_TRUE(snapshot_1.Get(key_6, 300));
  EXPECT_EQ(snapshot_1.Get(key_6, 300).payload(), PayloadHandle{30});
  EXPECT_EQ(snapshot_1.Get(key_6, 300).version(), 1);

  // Checking the new snapshot
  EXPECT_EQ(snapshot_2.version(), 2);
  EXPECT_EQ(snapshot_2.keys_count(), 2);
  EXPECT_EQ(snapshot_2.subkeys_count(), 2);
  EXPECT_EQ(snapshot_2.GetSubkeysCount(key_5), 1);
  EXPECT_EQ(snapshot_2.GetSubkeysCount(key_6), 1);

  ASSERT_TRUE(snapshot_2.Get(key_5, 300));
  EXPECT_EQ(snapshot_2.Get(key_5, 300).payload(), PayloadHandle{3});
  EXPECT_EQ(snapshot_2.Get(key_5, 300).version(), 2);

  ASSERT_TRUE(snapshot_2.Get(key_6, 200));
  EXPECT_EQ(snapshot_2.Get(key_6, 200).payload(), PayloadHandle{20});
  // Didn't change since the previous version.
  EXPECT_EQ(snapshot_2.Get(key_6, 200).version(), 1);

  EXPECT_FALSE(snapshot_2.Get(key_5, 100));
  EXPECT_FALSE(snapshot_2.Get(key_5, 200));
  EXPECT_FALSE(snapshot_2.Get(key_6, 100));

  {
    KeyIterator key_it = snapshot_2.begin();
    ASSERT_NE(key_it, snapshot_2.end());
    EXPECT_EQ(key_it->key_handle(), KeyHandle{5});
    EXPECT_EQ(key_it->subkeys_count(), 1);
    {
      auto subkeys = snapshot_2.GetSubkeys(*key_it);

      SubkeyIterator it = subkeys.begin();
      ASSERT_NE(it, subkeys.end());
      EXPECT_EQ(it->subkey(), 300);
      EXPECT_EQ(it->payload(), PayloadHandle{3});
      EXPECT_EQ(it->version(), 2);
      ++it;
      ASSERT_EQ(it, subkeys.end());
    }
    ++key_it;
    ASSERT_NE(key_it, snapshot_2.end());
    EXPECT_EQ(key_it->key_handle(), KeyHandle{6});
    EXPECT_EQ(key_it->subkeys_count(), 1);
    {
      auto subkeys = snapshot_2.GetSubkeys(*key_it);

      SubkeyIterator it = subkeys.begin();
      ASSERT_NE(it, subkeys.end());
      EXPECT_EQ(it->subkey(), 200);
      EXPECT_EQ(it->payload(), PayloadHandle{20});
      EXPECT_EQ(it->version(), 1);
      ++it;
      ASSERT_EQ(it, subkeys.end());
    }
    ++key_it;
    ASSERT_EQ(key_it, snapshot_2.end());
  }
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
