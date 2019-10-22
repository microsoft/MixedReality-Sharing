// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Storage.h>

#include "src/HeaderBlock.h"
#include "src/TransactionLayout.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamReader.h>
#include <Microsoft/MixedReality/Sharing/Common/Serialization/MonotonicSequenceEncoder.h>

#include <limits>
#include <type_traits>
#include <utility>
#include <vector>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {
namespace {

struct PendingSubkeyTransaction {
  PendingSubkeyTransaction(uint64_t subkey,
                           const SubkeyTransactionView& transaction_view,
                           const SubkeyStateAndIndexView& state_and_index_view)
      : subkey_{subkey},
        transaction_view_{transaction_view},
        state_and_index_view_{state_and_index_view} {}

  uint64_t subkey_;
  SubkeyTransactionView transaction_view_;
  SubkeyStateAndIndexView state_and_index_view_;
};

struct PendingKeyTransaction {
  PendingKeyTransaction(const KeyStateAndIndexView& view,
                        bool clear_before_transaction) noexcept
      : state_and_index_view_{view},
        subkeys_count_before_{view.latest_subkeys_count_thread_unsafe()},
        clear_before_transaction_{clear_before_transaction} {}

  bool Validate(const KeyTransactionView& key_transaction_view) const noexcept {
    return !key_transaction_view.required_subkeys_count_ ||
           *key_transaction_view.required_subkeys_count_ ==
               subkeys_count_before_;
  }

  constexpr bool subkeys_count_changed() const noexcept {
    return subkeys_count_before_ != subkeys_count_after_;
  }

  constexpr bool new_subkeys_count_satisfies_requirements() const noexcept {
    // This limitation is imposed by the current implementation that uses 32-bit
    // offsets for blocks and stores the number of subkeys as uint32_t.
    // TODO: add support for user-defined requirements for the subkeys count
    // after the transaction.
    return subkeys_count_after_ <= std::numeric_limits<uint32_t>::max();
  }

  KeyStateAndIndexView state_and_index_view_;
  uint32_t subkeys_count_before_;
  bool clear_before_transaction_;
  size_t subkeys_count_after_{0};
  size_t subkey_transactions_count_{0};
  std::optional<KeyHandle> owned_key_handle_;
};

class TransactionApplicator {
 public:
  TransactionApplicator(std::shared_ptr<Behavior> behavior,
                        MutatingBlobAccessor& accessor) noexcept
      : behavior_{std::move(behavior)},
        accessor_{accessor},
        new_version_{accessor.next_version()},
        is_version_added_{accessor.AddVersion()},
        is_allocation_failed_{!is_version_added_} {}

  ~TransactionApplicator() noexcept { Clear(); }

  std::pair<Snapshot, Storage::TransactionResult> Apply(
      TransactionView& transaction) noexcept {
    if (Prepare(transaction)) {
      if (is_allocation_failed_ ||
          !accessor_.CanInsertStateBlocks(extra_blocks_count_)) {
        return CreateMergedBlob(Storage::TransactionResult::Applied);
      }
      return {ApplyToExistingBlob(), Storage::TransactionResult::Applied};
    }
    Clear();
    return CreateMergedBlob(
        Storage::TransactionResult::
            AppliedWithNoEffectDueToUnsatisfiedPrerequisites);
  }

  bool Prepare(TransactionView& transaction) noexcept {
    // FIXME: ensure that reserve casts are safe on all platforms.
    key_transactions_.reserve(
        static_cast<size_t>(transaction.mentioned_keys_count()));
    if (auto hint = transaction.mentioned_subkeys_count_hint())
      subkey_transactions_.reserve(static_cast<size_t>(hint));

    while (transaction.MoveNextKey()) {
      const KeyTransactionView key_transaction_view =
          transaction.GetKeyTransactionView();

      // It's possible that we'll pop this element later, if it turns out
      // that no subkeys need changes.
      PendingKeyTransaction& pending_key_transaction =
          key_transactions_.emplace_back(
              accessor_.FindKeyStateAndIndex(
                  key_transaction_view.key_descriptior_),
              key_transaction_view.clear_before_transaction_);

      if (!pending_key_transaction.Validate(key_transaction_view))
        return false;

      if (key_transaction_view.clear_before_transaction_) {
        // Using the property that all subkeys in the transaction are sorted,
        // iterating over both collections of subkeys simultaneously (the
        // existing ones and the ones mentioned in the transaction).
        // The ones that are not mentioned in the transaction should be removed
        // unconditionally, others should be validated. We can't start applying
        // the new version before the whole validation process is finished, but
        // we can reserve space as we go.
        auto state_subkeys_it =
            accessor_.GetSubkeys(pending_key_transaction.state_and_index_view_)
                .begin();

        while (transaction.MoveNextSubkey()) {
          const uint64_t transaction_subkey = transaction.current_subkey();
          while (!state_subkeys_it.is_end() &&
                 state_subkeys_it->subkey() < transaction_subkey) {
            PrepareCleanupAndAdvance(state_subkeys_it);
          }
          SubkeyStateAndIndexView current_subkey_view;
          if (!state_subkeys_it.is_end() &&
              state_subkeys_it->subkey() == transaction_subkey) {
            current_subkey_view = *state_subkeys_it;
            ++state_subkeys_it;
          }
          const VersionedPayloadHandle current_payload =
              current_subkey_view.latest_payload_thread_unsafe();
          SubkeyTransactionView subkey_transaction_view =
              transaction.GetSubkeyTransactionView(current_payload);

          if (subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::ValidationFailed)
            return false;
          auto& subkey_transaction = EmplaceSubkeyTransaction(
              transaction_subkey, subkey_transaction_view, current_subkey_view);
          if (subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::NoChangeRequired) {
            if (current_payload)
              ++pending_key_transaction.subkeys_count_after_;
            continue;
          }
          const bool has_new_payload =
              subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::PutSubkey;
          if (has_new_payload)
            ++pending_key_transaction.subkeys_count_after_;

          if (!current_subkey_view) {
            assert(subkey_transaction_view.operation_ ==
                   SubkeyTransactionView::Operation::PutSubkey);
            ++extra_blocks_count_;
          } else if (!is_allocation_failed_) {
            is_allocation_failed_ = !accessor_.ReserveSpaceForTransaction(
                subkey_transaction.state_and_index_view_, new_version_,
                has_new_payload);
          }
        }
        while (!state_subkeys_it.is_end())
          PrepareCleanupAndAdvance(state_subkeys_it);
      } else {
        // Will be updated as we traverse through subkey transactions.
        pending_key_transaction.subkeys_count_after_ =
            pending_key_transaction.subkeys_count_before_;

        while (transaction.MoveNextSubkey()) {
          const uint64_t transaction_subkey = transaction.current_subkey();

          SubkeyStateAndIndexView current_subkey_view;
          if (pending_key_transaction.state_and_index_view_) {
            current_subkey_view = accessor_.FindSubkeyStateAndIndex(
                key_transaction_view.key_descriptior_, transaction_subkey);
          }

          const VersionedPayloadHandle current_payload =
              current_subkey_view.latest_payload_thread_unsafe();
          SubkeyTransactionView subkey_transaction_view =
              transaction.GetSubkeyTransactionView(current_payload);

          if (subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::ValidationFailed)
            return false;
          if (subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::NoChangeRequired)
            continue;

          auto& subkey_transaction = EmplaceSubkeyTransaction(
              transaction_subkey, subkey_transaction_view, current_subkey_view);

          const bool has_new_payload =
              subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::PutSubkey;
          if (subkey_transaction_view.operation_ ==
              SubkeyTransactionView::Operation::RemoveSubkey) {
            assert(pending_key_transaction.subkeys_count_after_ > 0);
            --pending_key_transaction.subkeys_count_after_;
          } else if (!current_payload) {
            ++pending_key_transaction.subkeys_count_after_;
            assert(pending_key_transaction.subkeys_count_after_ != 0);
          }
          if (!current_subkey_view) {
            assert(subkey_transaction_view.operation_ ==
                   SubkeyTransactionView::Operation::PutSubkey);
            ++extra_blocks_count_;
          } else if (!is_allocation_failed_) {
            is_allocation_failed_ = !accessor_.ReserveSpaceForTransaction(
                subkey_transaction.state_and_index_view_, new_version_,
                has_new_payload);
          }
        }
      }
      if (!pending_key_transaction.new_subkeys_count_satisfies_requirements())
        return false;

      if (pending_key_transaction.subkey_transactions_count_ == 0 &&
          !pending_key_transaction.subkeys_count_changed()) {
        // There are no actual actions associated with this key.
        // Either it was here only for validation reasons, or all subkey
        // operations ended up being NoChangeRequired.
        key_transactions_.pop_back();
      } else if (!pending_key_transaction.state_and_index_view_) {
        // We won't be iterating over the original transaction object's keys
        // again after the preparation step, so it's safe to make a
        // potentially destructive MakeHandle() call (which can transfer the
        // ownership to the handle).
        pending_key_transaction.owned_key_handle_ =
            key_transaction_view.key_descriptior_.MakeHandle();
        ++extra_blocks_count_;
      } else if (!is_allocation_failed_ &&
                 pending_key_transaction.subkeys_count_changed()) {
        is_allocation_failed_ = !accessor_.ReserveSpaceForTransaction(
            pending_key_transaction.state_and_index_view_);
      }
    }
    return true;
  }

  struct KeyBlockFlags {
    bool should_survive = false;
    bool is_clear_before_transaction_mode = false;
  };

  static KeyBlockFlags GetKeyBlockFlags(
      const KeyStateView& key_state_view) noexcept {
    const KeyStateBlock& key_block = *key_state_view.state_block_;
    // Blocks with subscriptions survive unconditionally because we want to
    // preserve the subscription even if there are no subkeys.
    bool should_survive = key_block.has_subscription();
    bool is_clear_before_transaction_mode = false;
    if (key_block.is_scratch_buffer_mode()) {
      auto tx = reinterpret_cast<PendingKeyTransaction*>(
          key_block.GetScratchBuffer());
      if (tx->subkeys_count_after_) {
        should_survive = true;
      }
      is_clear_before_transaction_mode = tx->clear_before_transaction_;
    } else if (key_state_view.latest_subkeys_count_thread_unsafe()) {
      should_survive = true;
    }
    return {should_survive, is_clear_before_transaction_mode};
  }

 private:
  void PrepareCleanupAndAdvance(SubkeyBlockIterator& it) noexcept {
    if (!is_allocation_failed_) {
      SubkeyStateAndIndexView view = *it;
      if (view.has_payload_thread_unsafe()) {
        is_allocation_failed_ =
            !accessor_.ReserveSpaceForTransaction(view, new_version_, false);
      }
    }
    ++it;
  }

  void Publish(PendingSubkeyTransaction& transaction,
               KeyStateBlock& key_block) noexcept {
    // In case of NoChangeRequired this PendingSubkeyTransaction was here as a
    // placeholder to prevent the cleanup before the transaction.
    if (transaction.transaction_view_.operation_ !=
        SubkeyTransactionView::Operation::NoChangeRequired) {
      auto new_handle = transaction.transaction_view_.ReleaseHandle();
      if (SubkeyVersionBlock* block =
              transaction.state_and_index_view_.version_block_) {
        block->PushFromWriterThread(new_version_, new_handle);
      } else {
        if (!transaction.state_and_index_view_.state_block_) {
          transaction.state_and_index_view_ = accessor_.InsertSubkeyBlock(
              *behavior_, key_block, transaction.subkey_);
        }
        transaction.state_and_index_view_.state_block_->PushFromWriterThread(
            new_version_, new_handle);
      }
    }
  };

  PendingSubkeyTransaction& EmplaceSubkeyTransaction(
      uint64_t subkey,
      const SubkeyTransactionView& transaction_view,
      const SubkeyStateAndIndexView& subkey_state_and_index_view) noexcept {
    auto& subkey_transaction = subkey_transactions_.emplace_back(
        subkey, transaction_view, subkey_state_and_index_view);
    ++key_transactions_.back().subkey_transactions_count_;
    return subkey_transaction;
  }

  static bool SubkeyBlockShouldSurvive(
      const SubkeyStateView& state_view,
      bool is_clear_before_transaction_mode) noexcept {
    if (state_view.state_block_->has_subscription()) {
      // Subkeys with subscriptions survive unconditionally because we want
      // to preserve the subscription even if there is no alive payload.
      return true;
    }
    if (!state_view.state_block_->is_scratch_buffer_mode())
      return !is_clear_before_transaction_mode &&
             state_view.has_payload_thread_unsafe();

    // The pointer could be saved to the scratch buffer only by the first
    // loop block of CreateMergedBlob, so we know which type this is.
    SubkeyTransactionView& subkey_transaction_view =
        reinterpret_cast<PendingSubkeyTransaction*>(
            state_view.state_block_->GetScratchBuffer())
            ->transaction_view_;
    switch (subkey_transaction_view.operation_) {
      case SubkeyTransactionView::Operation::PutSubkey:
        return true;
      case SubkeyTransactionView::Operation::RemoveSubkey:
        return false;
      default:
        // This transaction exists to suppress is_clear_before_transaction_mode.
        assert(subkey_transaction_view.operation_ ==
               SubkeyTransactionView::Operation::NoChangeRequired);
        return state_view.has_payload_thread_unsafe();
    }
  }

  // Writes PendingKeyTransaction/PendingSubkeyTransaction to the scratch buffer
  // area of state blocks (so that we'll know which blocks are affected by the
  // transaction).
  void InitScratchBuffers() noexcept {
    auto it = subkey_transactions_.begin();
    for (PendingKeyTransaction& key_tx : key_transactions_) {
      if (key_tx.state_and_index_view_) {
        key_tx.state_and_index_view_.state_block_->SetScratchBuffer(&key_tx);
        for (const auto it_end = next(it, key_tx.subkey_transactions_count_);
             it != it_end; ++it) {
          if (it->state_and_index_view_)
            it->state_and_index_view_.state_block_->SetScratchBuffer(&*it);
        }
      } else {
        advance(it, key_tx.subkey_transactions_count_);
      }
    }
  }

  size_t CountRequiredBlocksForMerge() const noexcept {
    size_t required_blocks_count = extra_blocks_count_;
    for (auto&& key_state_view : accessor_) {
      KeyBlockFlags key_flags = GetKeyBlockFlags(key_state_view);
      for (SubkeyStateAndIndexView subkey_state_view :
           accessor_.GetSubkeys(key_state_view)) {
        if (SubkeyBlockShouldSurvive(
                subkey_state_view,
                key_flags.is_clear_before_transaction_mode)) {
          ++required_blocks_count;
          key_flags.should_survive = true;
        }
      }
      if (key_flags.should_survive)
        ++required_blocks_count;
    }
    return required_blocks_count;
  }

  VersionedPayloadHandle GetMergedPayloadHandle(
      const SubkeyStateView& state_view,
      bool is_clear_before_transaction_mode) noexcept {
    bool delete_if_unchanged = is_clear_before_transaction_mode;
    if (state_view.state_block_->is_scratch_buffer_mode()) {
      SubkeyTransactionView& subkey_transaction_view =
          reinterpret_cast<PendingSubkeyTransaction*>(
              state_view.state_block_->GetScratchBuffer())
              ->transaction_view_;
      switch (subkey_transaction_view.operation_) {
        case SubkeyTransactionView::Operation::PutSubkey:
          return {new_version_, *subkey_transaction_view.ReleaseHandle()};
        case SubkeyTransactionView::Operation::RemoveSubkey:
          return {};
      }
      assert(subkey_transaction_view.operation_ ==
             SubkeyTransactionView::Operation::NoChangeRequired);
      // This transaction exists to suppress is_clear_before_transaction_mode.
      delete_if_unchanged = false;
    }
    if (!delete_if_unchanged) {
      if (auto payload = state_view.latest_payload_thread_unsafe()) {
        return {payload.version(),
                behavior_->DuplicateHandle(payload.payload())};
      }
    }
    return {};
  }

  std::pair<Snapshot, Storage::TransactionResult> CreateMergedBlob(
      Storage::TransactionResult successful_result) noexcept {
    if (is_version_added_) {
      accessor_.header_block().RemoveSnapshotReference(new_version_,
                                                       *behavior_);
    }
    accessor_.SetImmutableMode();
    InitScratchBuffers();
    const size_t required_blocks_count = CountRequiredBlocksForMerge();
    HeaderBlock* new_header_block = HeaderBlock::CreateBlob(
        *behavior_, new_version_, required_blocks_count * 2);
    if (!new_header_block)
      return {Snapshot{},
              Storage::TransactionResult::FailedDueToInsufficientResources};

    MutatingBlobAccessor new_accessor{*new_header_block};

    // First, moving all surviving blocks from the existing blob.
    for (auto&& old_key_state_view : accessor_) {
      auto key_flags = GetKeyBlockFlags(old_key_state_view);
      KeyStateBlock* new_key_state_block = nullptr;
      auto EnsureNewKeyBlockExists = [&] {
        if (!new_key_state_block) {
          auto& old_key_block = *old_key_state_view.state_block_;
          KeyStateAndIndexView key_state_view = new_accessor.InsertKeyBlock(
              *behavior_, behavior_->DuplicateHandle(old_key_block.key_));
          new_key_state_block = key_state_view.state_block_;
          assert(new_key_state_block);
          if (old_key_block.has_subscription()) {
            assert(!"Not implemented yet");
          }
          uint32_t new_subkeys_count = 0;
          if (old_key_block.is_scratch_buffer_mode()) {
            // The pointer is set by the first loop of CreateMergedBlob().
            auto tx = reinterpret_cast<PendingKeyTransaction*>(
                old_key_block.GetScratchBuffer());
            // Replacing the old state view with the new one.
            tx->state_and_index_view_ = key_state_view;
            // This was already checked in Prepare(), and it guarantees that
            // it's safe to cast to uint32_t below.
            assert(tx->new_subkeys_count_satisfies_requirements());
            new_subkeys_count = static_cast<uint32_t>(tx->subkeys_count_after_);
          } else {
            new_subkeys_count =
                old_key_state_view.latest_subkeys_count_thread_unsafe();
          }
          if (new_subkeys_count) {
            new_key_state_block->PushSubkeysCountFromWriterThread(
                VersionOffset{0}, new_subkeys_count);
            ++new_accessor.keys_count();
          }
        }
      };
      if (key_flags.should_survive)
        EnsureNewKeyBlockExists();
      for (SubkeyStateAndIndexView subkey_state_view :
           accessor_.GetSubkeys(old_key_state_view)) {
        SubkeyStateBlock& old_subkey_state_block =
            *subkey_state_view.state_block_;
        if (SubkeyBlockShouldSurvive(
                subkey_state_view,
                key_flags.is_clear_before_transaction_mode)) {
          EnsureNewKeyBlockExists();
          const uint64_t subkey = old_subkey_state_block.subkey_;
          SubkeyStateBlock* new_subkey_state_block =
              new_accessor
                  .InsertSubkeyBlock(*behavior_, *new_key_state_block, subkey)
                  .state_block_;
          assert(new_subkey_state_block);
          if (old_subkey_state_block.has_subscription()) {
            // Should move the subscription to the new block.
            assert(!"Not implemented yet");
          }
          if (VersionedPayloadHandle handle = GetMergedPayloadHandle(
                  subkey_state_view,
                  key_flags.is_clear_before_transaction_mode)) {
            new_subkey_state_block->PushFromWriterThread(handle.version(),
                                                         handle.payload());
            ++new_accessor.subkeys_count();
          }
        }
      }
    }
    // Now handling keys and subkeys from the new transaction that were not
    // touched by the code above.
    auto it = subkey_transactions_.begin();
    for (PendingKeyTransaction& key_tx : key_transactions_) {
      // Can be nullptr at this point. If the block was already found, the
      // loop above would replace it with a state block from the new blob.
      KeyStateBlock* new_key_state_block =
          key_tx.state_and_index_view_.state_block_;
      auto EnsureNewKeyBlockExists = [&] {
        if (!new_key_state_block) {
          // Transferring the ownership
          assert(key_tx.owned_key_handle_);
          key_tx.state_and_index_view_ = new_accessor.InsertKeyBlock(
              *behavior_, *key_tx.owned_key_handle_);
          assert(key_tx.state_and_index_view_);
          key_tx.owned_key_handle_.reset();
          new_key_state_block = key_tx.state_and_index_view_.state_block_;
          if (auto count = key_tx.subkeys_count_after_) {
            // This was already checked in Prepare()
            assert(key_tx.new_subkeys_count_satisfies_requirements());
            new_key_state_block->PushSubkeysCountFromWriterThread(
                VersionOffset{0}, static_cast<uint32_t>(count));
            ++new_accessor.keys_count();
          }
        }
      };
      for (const auto it_end = next(it, key_tx.subkey_transactions_count_);
           it != it_end; ++it) {
        PendingSubkeyTransaction& sub_tx = *it;
        // We are only interested in subkeys that were not handled when we
        // were traversing over all existing keys and subkeys.
        if (!sub_tx.state_and_index_view_) {
          assert(sub_tx.transaction_view_.operation_ ==
                 SubkeyTransactionView::Operation::PutSubkey);
          EnsureNewKeyBlockExists();
          sub_tx.state_and_index_view_ = new_accessor.InsertSubkeyBlock(
              *behavior_, *new_key_state_block, sub_tx.subkey_);

          sub_tx.state_and_index_view_.state_block_->PushFromWriterThread(
              new_version_, sub_tx.transaction_view_.ReleaseHandle());
          ++new_accessor.subkeys_count();
        }
      }
    }
    return {
        {new_version_, new_accessor.header_block_, new_accessor.keys_count(),
         new_accessor.subkeys_count(), std::move(behavior_)},
        successful_result};
  }

  Snapshot ApplyToExistingBlob() noexcept {
    VersionOffset version_offset =
        MakeVersionOffset(new_version_, accessor_.base_version());
    auto it = subkey_transactions_.begin();
    for (PendingKeyTransaction& pending_key_transaction : key_transactions_) {
      if (!pending_key_transaction.state_and_index_view_) {
        assert(pending_key_transaction.owned_key_handle_);
        pending_key_transaction.state_and_index_view_ =
            accessor_.InsertKeyBlock(
                *behavior_, *pending_key_transaction.owned_key_handle_);
        assert(pending_key_transaction.state_and_index_view_);
        pending_key_transaction.owned_key_handle_.reset();
      }
      KeyStateBlock& key_block =
          *pending_key_transaction.state_and_index_view_.state_block_;
      if (pending_key_transaction.subkeys_count_changed()) {
        // This was already checked in Prepare(), and it guarantees that
        // it's safe to cast to uint32_t below.
        assert(
            pending_key_transaction.new_subkeys_count_satisfies_requirements());
        const uint32_t new_subkeys_count =
            static_cast<uint32_t>(pending_key_transaction.subkeys_count_after_);

        if (new_subkeys_count == 0) {
          --accessor_.keys_count();
        } else if (pending_key_transaction.subkeys_count_before_ == 0) {
          ++accessor_.keys_count();
        }
        assert(static_cast<uint64_t>(accessor_.subkeys_count()) +
                   new_subkeys_count >=
               pending_key_transaction.subkeys_count_before_);
        accessor_.subkeys_count() +=
            new_subkeys_count - pending_key_transaction.subkeys_count_before_;
        if (KeyVersionBlock* block =
                pending_key_transaction.state_and_index_view_.version_block_) {
          block->PushSubkeysCountFromWriterThread(version_offset,
                                                  new_subkeys_count);
        } else {
          key_block.PushSubkeysCountFromWriterThread(version_offset,
                                                     new_subkeys_count);
        }
      }
      const auto it_end =
          next(it, pending_key_transaction.subkey_transactions_count_);
      if (pending_key_transaction.clear_before_transaction_) {
        for (SubkeyStateAndIndexView subkey_state_view : accessor_.GetSubkeys(
                 pending_key_transaction.state_and_index_view_)) {
          uint64_t subkey = subkey_state_view.subkey();
          bool already_handled = false;
          for (; it != it_end && it->subkey_ <= subkey; ++it) {
            already_handled = it->subkey_ == subkey;
            Publish(*it, key_block);
          }
          if (!already_handled)
            subkey_state_view.EnsureDeletedFromWriterThread(new_version_);
        }
      }
      for (; it != it_end; ++it)
        Publish(*it, key_block);
    }
    return {new_version_, accessor_.header_block_, accessor_.keys_count(),
            accessor_.subkeys_count(), std::move(behavior_)};
  }

  void Clear() {
    for (auto& transaction : subkey_transactions_) {
      if (auto handle = transaction.transaction_view_.ReleaseHandle())
        behavior_->Release(*handle);
    }
    subkey_transactions_.clear();
    for (auto& transaction : key_transactions_) {
      if (transaction.owned_key_handle_)
        behavior_->Release(*transaction.owned_key_handle_);
    }
    key_transactions_.clear();
  }

  std::shared_ptr<Behavior> behavior_;
  MutatingBlobAccessor& accessor_;
  const uint64_t new_version_;
  bool is_version_added_;
  bool is_allocation_failed_;

  std::vector<PendingKeyTransaction> key_transactions_;
  std::vector<PendingSubkeyTransaction> subkey_transactions_;
  size_t extra_blocks_count_{0};
};

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
}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

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
    TransactionView& transaction) noexcept {
  auto writer_mutex_guard = Detail::WriterMutexGuard(*behavior_);
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
  Detail::TransactionApplicator applicator{behavior_, accessor};
  auto pair = applicator.Apply(transaction);
  if (pair.second == TransactionResult::FailedDueToInsufficientResources)
    return TransactionResult::AppliedWithNoEffectDueToUnsatisfiedPrerequisites;

  auto lock = std::lock_guard{latest_snapshot_reader_mutex_};
  latest_snapshot_ = std::move(pair.first);
  return pair.second;
}

struct SafeBytestreamSizeCounter {};

class SerializedTransactionView : public TransactionView, public KeyDescriptor {
 public:
  SerializedTransactionView(Behavior& behavior,
                            std::string_view serialized_transaction)
      : KeyDescriptor{0}, behavior_{behavior}, reader_{serialized_transaction} {
    try {
      mentioned_keys_count_ = reader_.ReadExponentialGolombCode();
      Serialization::BitstreamReader preparse_reader{reader_};
      // The transaction consists of bit stream and byte stream (right after the
      // bit stream). We expect the sizes of all payloads to add up to the exact
      // size of the byte stream.
      uint64_t bytestream_content_size = 0;
      auto AddBytestreamContentSize = [&](auto value) {
        if (value <= ~0ull - bytestream_content_size) {
          bytestream_content_size += value;
          if (bytestream_content_size <=
              preparse_reader.untouched_bytes_count())
            return;
        }
        throw std::invalid_argument{"Can't decode a transaction"};
      };
      for (uint64_t key_id = 0; key_id < mentioned_keys_count_; ++key_id) {
        KeyTransactionLayout key_layout{preparse_reader};
        AddBytestreamContentSize(key_layout.key_size_);

        Serialization::MonotonicSequenceEncoder preparse_subkey_encoder;
        for (uint64_t subkey_id = 0; subkey_id < key_layout.subkeys_count_;
             ++subkey_id) {
          [[maybe_unused]] uint64_t subkey =
              preparse_subkey_encoder.DecodeNext(preparse_reader);
          SubkeyTransactionLayout layout{preparse_reader};
          AddBytestreamContentSize(layout.bytestream_content_size());
        }
        mentioned_subkeys_count_ += key_layout.subkeys_count_;
      }
      size_t untouched_bytes_count = preparse_reader.untouched_bytes_count();
      if (untouched_bytes_count != bytestream_content_size) {
        throw std::invalid_argument{
            "Can't decode a transaction: message size doesn't match the "
            "layout"};
      }
      next_data_ = serialized_transaction.data() +
                   (serialized_transaction.size() - untouched_bytes_count);
    } catch (const std::exception&) {
      assert(false);  // Not implemented yet
    }
  }

  uint64_t mentioned_keys_count() const noexcept override {
    return mentioned_keys_count_;
  }

  uint64_t mentioned_subkeys_count_hint() const noexcept override {
    return mentioned_subkeys_count_;
  }

  bool MoveNextKey() noexcept override {
    if (next_key_id_ == mentioned_keys_count_)
      return false;
    ++next_key_id_;
    current_key_layout_ = KeyTransactionLayout{reader_};
    current_serialized_key_ =
        ConsumeData(static_cast<size_t>(current_key_layout_.key_size_));
    key_hash_ = behavior_.GetKeyHash(current_serialized_key_);
    next_subkey_id_ = 0;
    subkey_encoder_.~MonotonicSequenceEncoder();
    new (&subkey_encoder_) Serialization::MonotonicSequenceEncoder{};
    return true;
  }

  bool MoveNextSubkey() noexcept override {
    if (next_subkey_id_ == current_key_layout_.subkeys_count_)
      return false;
    ++next_subkey_id_;
    current_subkey_ = subkey_encoder_.DecodeNext(reader_);
    static_assert(
        std::is_trivially_destructible_v<decltype(current_subkey_layout_)>);
    new (&current_subkey_layout_) SubkeyTransactionLayout{reader_};
    if (current_subkey_layout_.requirement_kind_ ==
        SubkeyTransactionRequirementKind::ExactPayload) {
      current_required_payload_ = ConsumeData(
          static_cast<size_t>(current_subkey_layout_.required_payload_size_));
    } else {
      current_required_payload_ = {};
    }
    if (current_subkey_layout_.action_kind_ ==
        SubkeyTransactionActionKind::PutSubkey) {
      current_new_payload_ = ConsumeData(
          static_cast<size_t>(current_subkey_layout_.new_payload_size_));
    }
    return true;
  }

  KeyTransactionView GetKeyTransactionView() noexcept override {
    assert(next_key_id_ != 0);
    return {*this, current_key_layout_.clear_before_transaction_,
            current_key_layout_.required_subkeys_count_};
  }

  SubkeyTransactionView GetSubkeyTransactionView(
      VersionedPayloadHandle current_state) noexcept override {
    assert(next_subkey_id_ != 0);
    if (!SatisfiesRequirements(current_state))
      return SubkeyTransactionView::Operation::ValidationFailed;

    if (!RequiresChange(current_state))
      return SubkeyTransactionView::Operation::NoChangeRequired;

    if (current_subkey_layout_.action_kind_ ==
        SubkeyTransactionActionKind::RemoveSubkey) {
      return SubkeyTransactionView::Operation::RemoveSubkey;
    }
    assert(current_subkey_layout_.action_kind_ ==
           SubkeyTransactionActionKind::PutSubkey);
    return behavior_.DeserializePayload(current_new_payload_);
  }

  bool IsEqualTo(KeyHandle key) const noexcept override {
    return behavior_.Equal(key, current_serialized_key_);
  }

  bool IsLessThan(KeyHandle key) const noexcept override {
    return behavior_.Less(current_serialized_key_, key);
  }

  bool IsGreaterThan(KeyHandle key) const noexcept override {
    return behavior_.Less(key, current_serialized_key_);
  }

  KeyHandle MakeHandle() noexcept override {
    return behavior_.DeserializeKey(current_serialized_key_);
  }

  KeyHandle MakeHandle(KeyHandle existing_handle) noexcept override {
    return behavior_.DuplicateHandle(existing_handle);
  }

 private:
  std::string_view ConsumeData(size_t size) noexcept {
    const char* data = next_data_;
    next_data_ += size;
    return {data, size};
  }

  bool SatisfiesRequirements(VersionedPayloadHandle current_state) const
      noexcept {
    switch (current_subkey_layout_.requirement_kind_) {
      case SubkeyTransactionRequirementKind::SubkeyExists:
        return current_state.has_payload();
      case SubkeyTransactionRequirementKind::SubkeyMissing:
        return !current_state.has_payload();
      case SubkeyTransactionRequirementKind::ExactVersion:
        return current_state.version() ==
               current_subkey_layout_.required_version_;
      case SubkeyTransactionRequirementKind::ExactPayload:
        return current_state && behavior_.Equal(current_state.payload(),
                                                current_required_payload_);
      default:
        return true;
    }
  }

  bool RequiresChange(VersionedPayloadHandle current_state) const noexcept {
    switch (current_subkey_layout_.action_kind_) {
      case SubkeyTransactionActionKind::PutSubkey:
        return !current_state.has_payload() ||
               !behavior_.Equal(current_state.payload(), current_new_payload_);
      case SubkeyTransactionActionKind::RemoveSubkey:
        return current_state.has_payload();
      default:
        return false;
    }
  }

  Behavior& behavior_;
  uint64_t mentioned_keys_count_{0};
  uint64_t mentioned_subkeys_count_{0};
  uint64_t next_key_id_{0};
  uint64_t next_subkey_id_{0};
  const char* next_data_{nullptr};
  Serialization::BitstreamReader reader_;
  Serialization::MonotonicSequenceEncoder subkey_encoder_;
  KeyTransactionLayout current_key_layout_;
  SubkeyTransactionLayout current_subkey_layout_;
  std::string_view current_serialized_key_;    // FIXME: set
  std::string_view current_required_payload_;  // FIXME: set
  std::string_view current_new_payload_;       // FIXME: set
};

Storage::TransactionResult Storage::ApplyTransaction(
    std::string_view serialized_transaction) noexcept {
  SerializedTransactionView transaction{*behavior_, serialized_transaction};
  return ApplyTransaction(transaction);
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
