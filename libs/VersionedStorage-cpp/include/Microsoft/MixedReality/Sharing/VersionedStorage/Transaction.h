// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/AbstractKey.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Behavior.h>

#include <memory>
#include <optional>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class HeaderBlock;
class Storage;

// An atomic modification of the storage which can be applied to it to transfer
// it to the next version. Transactions are destroyed after being applied.

// TODO: split into independent TransactionBuilder and Transaction.
class Transaction {
 public:
  virtual ~Transaction() noexcept = default;

  // The transaction will write new_payload to the provided subkey
  // (inserting it if it was missing).
  // The effect of any previous Delete call on the same subkey within this
  // transaction is canceled.
  virtual void Put(AbstractKey& key,
                   uint64_t subkey,
                   PayloadHandle new_payload) noexcept = 0;

  // The transaction will delete the specified subkey if it exists at the moment
  // of application.
  // The effect of any previous Put call on the same subkey within this
  // transaction is canceled.
  virtual void Delete(AbstractKey& key, uint64_t subkey) noexcept = 0;

  // All existing subkeys will be deleted before inserting any subkeys specified
  // by Put calls.
  // Note that the deletion will happen after checking the prerequisites
  // specified with Require calls.
  virtual void ClearBeforeTransaction(AbstractKey& key) noexcept = 0;

  // Applying the transaction will fail unless the value of the subkey is equal
  // to the required_payload.
  virtual void RequirePayload(AbstractKey& key,
                              uint64_t subkey,
                              PayloadHandle required_payload) noexcept = 0;

  // Applying the transaction will fail unless the subkey is missing.
  // The effect of any previous RequirePayload call on the same subkey within
  // this transaction is canceled.
  virtual void RequireMissingSubkey(AbstractKey& key,
                                    uint64_t subkey) noexcept = 0;

  // Applying the transaction will fail unless the number of subkeys is equal to
  // required_subkeys_count.
  // The effect of any previous RequireMissingSubkey call on the same subkey
  // within this transaction is canceled.
  virtual void RequireSubkeysCount(AbstractKey& key,
                                   size_t required_subkeys_count) noexcept = 0;

  static std::unique_ptr<Transaction> Create(
      std::shared_ptr<Behavior> behavior) noexcept;

 protected:
  Transaction() noexcept = default;

  Transaction(const Transaction&) = delete;
  Transaction& operator=(const Transaction&) = delete;

  friend class Storage;

  enum class PrepareResult {
    Ready,
    ValidationFailed,
    AllocationFailed,
  };

  // TODO: accept an accessor
  [[nodiscard]] virtual PrepareResult Prepare(
      uint64_t new_version,
      HeaderBlock& header_block,
      size_t& extra_blocks_count) noexcept = 0;

  virtual void Apply(uint64_t new_version,
                     HeaderBlock& header_block) noexcept = 0;

  [[nodiscard]] virtual HeaderBlock* CreateMergedBlob(
      uint64_t new_version,
      HeaderBlock& existing_header_block,
      size_t extra_states_to_insert) noexcept = 0;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
