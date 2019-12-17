// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Behavior.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptor.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/VersionedPayloadHandle.h>

#include <memory>
#include <optional>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::Serialization {
class BitstreamReader;
class BitstreamWriter;
}  // namespace Microsoft::MixedReality::Sharing::Serialization

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// A view of a key mentioned in the transaction.
// key_descriptior_ is only valid until the next call of MoveNextKey()
// (the implementation may reuse the same internal KeyDescriptor object to
// present the next key).
struct KeyTransactionView {
  KeyDescriptor& key_descriptior_;
  bool clear_before_transaction_;
  std::optional<uint64_t> required_subkeys_count_;
};

struct SubkeyTransactionView {
  enum class Operation {
    ValidationFailed,  // The observed subkey doesn't satisfy the prerequisites
    NoChangeRequired,
    PutSubkey,
    RemoveSubkey,
  };

  SubkeyTransactionView(Operation subkey_operation) noexcept
      : operation_{subkey_operation} {
    assert(operation_ != Operation::PutSubkey);
  }

  // Takes the ownership
  SubkeyTransactionView(PayloadHandle handle) noexcept
      : operation_{Operation::PutSubkey}, handle_{handle} {}

  // If this view owns a handle, transfers the ownership to the caller.
  std::optional<PayloadHandle> ReleaseHandle() noexcept {
    if (operation_ == Operation::PutSubkey) {
      operation_ = Operation::NoChangeRequired;
      return handle_;
    }
    return {};
  }

  Operation operation_;

 private:
  // Only valid if operation_ is PutSubkey.
  // SubkeyTransactionView object owns this PayloadHandle.
  PayloadHandle handle_{0};
};

// Enumerator-like interface for exploring a transaction
// (possibly in serialized form, without unpacking).
class TransactionView {
 public:
  // The number of keys that will be mentioned by this TransactionView
  virtual uint64_t mentioned_keys_count() const noexcept = 0;

  // The approximate hint about the number of subkeys that will be mentioned
  // by this TransactionView. Can be off in any direction, and should only be
  // used for reserve() calls. The implementation is allowed to return 0 if it
  // doesn't have this number.
  virtual uint64_t mentioned_subkeys_count_hint() const noexcept = 0;

  virtual bool MoveNextKey() noexcept = 0;
  virtual bool MoveNextSubkey() noexcept = 0;

  // The result is valid only until the next MoveNextKey() call.
  virtual KeyTransactionView GetKeyTransactionView() noexcept = 0;

  // Should only be called after a successful MoveNextSubkey()
  // and before the next MoveNextKey().
  constexpr uint64_t current_subkey() const noexcept { return current_subkey_; }

  virtual SubkeyTransactionView GetSubkeyTransactionView(
      VersionedPayloadHandle current_state) noexcept = 0;

 protected:
  uint64_t current_subkey_{0};
};

// An atomic modification of the storage which can be applied to it to transfer
// it to the next version. Transactions are destroyed after being applied.
class TransactionBuilder : public TransactionView {
 public:
  virtual ~TransactionBuilder() noexcept = default;

  // Instructs the transaction to write the value to the subkey of the key.
  // The effect of any previous Put() or Delete() calls on the same subkey
  // within this transaction will be overwritten by this call. When the
  // transaction is applied, the new value will overwrite any previous value
  // that this subkey had (or insert it if it was missing).
  virtual void Put(KeyDescriptor& key,
                   uint64_t subkey,
                   PayloadHandle new_payload) noexcept = 0;

  // Instructs the transaction to delete the subkey of the key.
  // The effect of any previous Put() or Delete() calls on the same subkey
  // within this transaction will be overwritten by this call.
  virtual void Delete(KeyDescriptor& key, uint64_t subkey) noexcept = 0;

  // Instructs the transaction to delete all existing subkeys of the key before
  // applying any other changes.
  // Subkeys will be removed after the prerequisites of the transaction are
  // checked, but before any other changes are applied.
  virtual void ClearBeforeTransaction(KeyDescriptor& key) noexcept = 0;

  // Instructs the transaction to check that the subkey is present before
  // applying the transaction. The transaction will fail if the subkey is
  // missing.
  // The effect of any previous Require* call on the same subkey within this
  // transaction will be overwritten by this call.
  virtual void RequirePresentSubkey(KeyDescriptor& key,
                                    uint64_t subkey) noexcept = 0;

  // Instructs the transaction to check that the subkey is missing before
  // applying the transaction. The transaction will fail if the subkey is
  // present.
  // The effect of any previous Require* call on the same subkey within this
  // transaction will be overwritten by this call.
  virtual void RequireMissingSubkey(KeyDescriptor& key,
                                    uint64_t subkey) noexcept = 0;

  // FIXME: rename
  // Instructs the transaction to check that the subkey has the provided value
  // before applying the transaction. The transaction will fail if the value is
  // different, or the subkey is missing.
  // The effect of any previous Require* call on the same subkey within this
  // transaction will be overwritten by this call.
  virtual void RequireExactPayload(KeyDescriptor& key,
                                   uint64_t subkey,
                                   PayloadHandle required_payload) noexcept = 0;

  // FIXME: rename
  // Instructs the transaction to check that the subkey has the provided
  // version before applying the transaction. The transaction will fail if the
  // version is different, or the subkey is missing.
  // The effect of any previous Require* call on the same subkey
  // within this transaction will be overwritten by this call.
  virtual void RequireExactVersion(KeyDescriptor& key,
                                   uint64_t subkey,
                                   uint64_t required_version) noexcept = 0;

  // Instructs the transaction to check that the number of subkeys of the key
  // is equal to the provided number.
  // The transaction will fail if the number of subkeys is different.
  // Any number is allowed, including 0 (to require that the entire key is
  // missing). The effect of any previous Require* call on the same subkey
  // within this transaction will be overwritten by this call.
  virtual void RequireSubkeysCount(KeyDescriptor& key,
                                   size_t required_subkeys_count) noexcept = 0;

  virtual void Serialize(Serialization::BitstreamWriter& bitstream_writer,
                         std::vector<std::byte>& byte_stream) noexcept = 0;

  static std::unique_ptr<TransactionBuilder> Create(
      std::shared_ptr<Behavior> behavior) noexcept;

 protected:
  TransactionBuilder() noexcept = default;

  TransactionBuilder(const TransactionBuilder&) = delete;
  TransactionBuilder& operator=(const TransactionBuilder&) = delete;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
