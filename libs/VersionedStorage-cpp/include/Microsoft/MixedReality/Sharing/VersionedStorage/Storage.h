// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Behavior.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Transaction.h>

#include <memory>
#include <mutex>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// A versioned snapshottable map-like data structure.
// It supports two-level addressing, with keys and subkeys within a key, where
// keys are abstract handles (see enums.h for details), and subkeys are
// uint64_t.
// Payloads associated with subkeys are abstract handles as well.
// The state of the storage changes atomically by applying transactions.
// To observe the state, a snapshot of the state must be taken (which is a
// relatively cheap operation). The state of the snapshot is then going to be
// immutable for the duration of its lifetime, even if new transactions were
// applied to the storage
class Storage {
 public:
  Storage(std::shared_ptr<Behavior> behavior);
  ~Storage();

  // Returns an immutable snapshot of the current state. Having multiple alive
  // snapshots does not prevent new transactions from being applied (in which
  // case calling GetSnapshot() will return a newer snapshot without affecting
  // the old ones). Any amount of snapshots can be alive at the same time (up to
  // the memory limit).
  // The snapshot itself is a thin object that just marks a certain versions in
  // the in-memory blobs as "in use," which ensures that the latest state with
  // the version less or equal to the "in use" one stays visible to readers.
  // For example, if a subkey had payload A on version 10, B on version 20, and
  // C on version 30, and the snapshot for version 25 is alive, the state B will
  // stay discoverable (because for this snapshot this is the actual state of
  // this subkey).
  std::shared_ptr<Snapshot> GetSnapshot() const noexcept;

  enum class TransactionResult {
    // The transaction is successfully applied and the version is incremented.
    Applied,

    // The operations in the transaction couldn't be applied due to unsatisfied
    // prerequisites, but the version was incremented anyway.
    AppliedWithNoEffectDueToUnsatisfiedPrerequisites,

    // The transaction couldn't be applied due to insufficient resources.
    // The version is not incremented since this this result could be specific
    // to this machine and not be reproducible under different conditions (and
    // thus incrementing the version could make the behavior non-deterministic),
    // and no further modifications can be made to the same state.
    // Old snapshots can still be safely observed.
    FailedDueToInsufficientResources,
  };

  // Applies the provided transaction and increments the version of the storage
  // (unless it failed to allocate memory for the transaction, see all possible
  // outcomes above).
  TransactionResult ApplyTransaction(
      std::unique_ptr<Transaction> transaction) noexcept;

 private:
  std::shared_ptr<Behavior> behavior_;
  std::shared_ptr<Snapshot> latest_snapshot_;
  mutable std::mutex latest_snapshot_reader_mutex_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
