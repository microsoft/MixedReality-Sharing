// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

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
      latest_snapshot_{
          std::make_shared<Snapshot>(0,
                                     *HeaderBlock::CreateBlob(*behavior_, 0, 0),
                                     behavior_)} {
  assert(behavior_);
}

Storage::~Storage() = default;

std::shared_ptr<Snapshot> Storage::GetSnapshot() const noexcept {
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
  HeaderBlock& current_header_block = latest_snapshot_->header_block_;

  if (!current_header_block.is_mutable_mode()) {
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

  bool has_added_version = current_header_block.AddVersion();

  Transaction::PrepareResult prepare_result =
      transaction->Prepare(new_version, current_header_block,
                           extra_blocks_count, !has_added_version);

  if (prepare_result == Transaction::PrepareResult::ValidationFailed) {
    if (has_added_version) {
      auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
      latest_snapshot_ = std::make_shared<Snapshot>(
          new_version, current_header_block, behavior_);
      return TransactionResult::
          AppliedWithNoEffectDueToUnsatisfiedPrerequisites;
    } else {
      // Creating a new empty transaction to merge it with the current state
      // into the new blob (we couldn't reuse the previous blob due to the lack
      // of space there to store the new version).
      HeaderBlock* new_header_block =
          Transaction::Create(behavior_)->CreateMergedBlob(
              new_version, current_header_block, 0);
      if (!new_header_block) {
        return Storage::TransactionResult::FailedDueToInsufficientResources;
      }
      auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
      latest_snapshot_ =
          std::make_shared<Snapshot>(new_version, *new_header_block, behavior_);
      return Storage::TransactionResult::
          AppliedWithNoEffectDueToUnsatisfiedPrerequisites;
    }
  }

  if (prepare_result == Transaction::PrepareResult::Ready &&
      has_added_version) {
    assert(current_header_block.CanInsertStateBlocks(extra_blocks_count));

    transaction->Apply(new_version, current_header_block);
    auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
    latest_snapshot_ = std::make_shared<Snapshot>(
        new_version, current_header_block, behavior_);
    return Storage::TransactionResult::Applied;
  }
  // This blob can't accept any new versions.
  if (has_added_version) {
    current_header_block.RemoveSnapshotReference(new_version, *behavior_);
  }

  current_header_block.SetImmutableMode();

  HeaderBlock* new_header_block = transaction->CreateMergedBlob(
      new_version, current_header_block, extra_blocks_count);

  if (!new_header_block) {
    return Storage::TransactionResult::FailedDueToInsufficientResources;
  }
  auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
  latest_snapshot_ =
      std::make_shared<Snapshot>(new_version, *new_header_block, behavior_);
  return Storage::TransactionResult::Applied;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
