// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Storage.h>

#include "src/HeaderBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace {
class WriterMutexGuard {
 public:
  explicit WriterMutexGuard(Behavior& behavior) noexcept : behavior_{behavior} {
    behavior.LockWriterMutex();
  }

  ~WriterMutexGuard() noexcept { behavior_.UnlockWriterMutex(); }

 private:
  Behavior& behavior_;
};
}  // namespace

Storage::Storage(std::shared_ptr<Behavior> behavior)
    : behavior_{std::move(behavior)},
      latest_snapshot_{0, *Detail::HeaderBlock::CreateBlob(*behavior_, 0, 0), 0,
                       0, behavior_} {
  assert(behavior_);
}

Storage::~Storage() = default;

Snapshot Storage::GetSnapshot() const noexcept {
  auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
  return latest_snapshot_;
}

Storage::TransactionResult Storage::ApplyTransaction(
    std::unique_ptr<Transaction> transaction) noexcept {
  assert(transaction);
  auto writer_mutex_guard = WriterMutexGuard(*behavior_);

  // No need to lock latest_snapshot_reader_mutex_ here to perform the read
  // since only the writer thread can modify the latest_snapshot_ field, and
  // this method is called by the writer thread.
  Detail::HeaderBlock& current_header_block = *latest_snapshot_.header_block_;

  Detail::MutatingBlobAccessor accessor{current_header_block};

  if (!accessor.is_mutable_mode()) {
    // This can happen if at some point this storage ran out of memory, but then
    // we failed to allocate the next block.
    // From now on, this storage can't make any progress.
    // The caller is expected to destroy the storage, free some resources, and
    // attempt to re-synchronize the state, if possible.
    return Storage::TransactionResult::FailedDueToInsufficientResources;
  }

  const uint64_t new_version = current_header_block.base_version() +
                               current_header_block.stored_versions_count();

  size_t extra_blocks_count = 0;

  bool has_added_version = accessor.AddVersion();

  Transaction::PrepareResult prepare_result = transaction->Prepare(
      new_version, accessor, extra_blocks_count, !has_added_version);

  if (prepare_result == Transaction::PrepareResult::ValidationFailed) {
    if (has_added_version) {
      auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
      latest_snapshot_ =
          Snapshot{new_version, current_header_block, accessor.keys_count(),
                   accessor.subkeys_count(), behavior_};
      return TransactionResult::
          AppliedWithNoEffectDueToUnsatisfiedPrerequisites;
    } else {
      // Creating a new empty transaction to merge it with the current state
      // into the new blob (we couldn't reuse the previous blob due to the lack
      // of space there to store the new version).
      Detail::HeaderBlock* new_header_block =
          Transaction::Create(behavior_)->CreateMergedBlob(new_version,
                                                           accessor, 0);
      if (!new_header_block) {
        return Storage::TransactionResult::FailedDueToInsufficientResources;
      }
      auto lock = std::lock_guard{latest_snapshot_reader_mutex_};

      Detail::MutatingBlobAccessor new_block_accessor{*new_header_block};

      latest_snapshot_ = Snapshot{
          new_version, *new_header_block, new_block_accessor.keys_count(),
          new_block_accessor.subkeys_count(), behavior_};
      return Storage::TransactionResult::
          AppliedWithNoEffectDueToUnsatisfiedPrerequisites;
    }
  }

  if (prepare_result == Transaction::PrepareResult::Ready &&
      has_added_version) {
    assert(accessor.CanInsertStateBlocks(extra_blocks_count));

    transaction->Apply(new_version, accessor);
    auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
    latest_snapshot_ =
        Snapshot{new_version, current_header_block, accessor.keys_count(),
                 accessor.subkeys_count(), behavior_};
    return Storage::TransactionResult::Applied;
  }
  // This blob can't accept any new versions.
  if (has_added_version) {
    current_header_block.RemoveSnapshotReference(new_version, *behavior_);
  }

  accessor.SetImmutableMode();

  Detail::HeaderBlock* new_header_block =
      transaction->CreateMergedBlob(new_version, accessor, extra_blocks_count);
  Detail::MutatingBlobAccessor new_block_accessor{*new_header_block};

  if (!new_header_block) {
    return Storage::TransactionResult::FailedDueToInsufficientResources;
  }
  auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
  latest_snapshot_ =
      Snapshot{new_version, *new_header_block, new_block_accessor.keys_count(),
               new_block_accessor.subkeys_count(), behavior_};
  return Storage::TransactionResult::Applied;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
