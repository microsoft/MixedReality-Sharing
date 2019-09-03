// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Transaction.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/AbstractKeyWithHandle.h>

#include "src/HeaderBlock.h"
#include "src/StateBlock.h"

// FIXME: no reason to use a slow std::map here.
#include <map>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace {

struct KeyTransaction;

struct SubkeyTransaction {
  OptionalPayloadStateOrDeletionMarker new_payload_handle_or_deletion_marker_;
  OptionalPayloadStateOrDeletionMarker
      required_payload_handle_or_deletion_marker_;
  SubkeyBlockStateSearchResult search_result_;

  SubkeyStateBlock* state_block() const noexcept {
    return search_result_.state_block_;
  }

  SubkeyVersionBlock* version_block() const noexcept {
    return search_result_.version_block_;
  }

  void ResetNewPayload(Behavior& behavior) noexcept {
    if (new_payload_handle_or_deletion_marker_.is_specific_handle()) {
      behavior.Release(*new_payload_handle_or_deletion_marker_);
    }
    new_payload_handle_or_deletion_marker_ = {};
  }

  void ResetRequiredPayload(Behavior& behavior) noexcept {
    if (required_payload_handle_or_deletion_marker_.is_specific_handle()) {
      behavior.Release(*required_payload_handle_or_deletion_marker_);
    }
    required_payload_handle_or_deletion_marker_ = {};
  }

  void Reset(Behavior& behavior) noexcept {
    ResetNewPayload(behavior);
    ResetRequiredPayload(behavior);
  }

  // Validates the preconditions and updates the transaction.
  // On success, the preconditions will be cleared, and if the subkey is already
  // in the required state, new_payload_handle_or_deletion_marker_ will be
  // cleared.
  // Should be called after obtaining the search result.
  bool InitializeAndValidate(Behavior& behavior,
                             KeyTransaction& key_transaction) noexcept;
};

using SubkeyTransactionsMap = std::map<uint64_t, SubkeyTransaction>;

struct KeyTransaction {
  ~KeyTransaction() noexcept { assert(owns_key_handle_ == false); }

  bool owns_key_handle_{true};
  bool clear_before_transaction_{false};
  std::optional<size_t> required_subkeys_count_;
  SubkeyTransactionsMap subkey_transactions_map_;
  KeyBlockStateSearchResult search_result_;

  uint32_t current_subkeys_count_{0};

  // Number of inserted subkeys that didn't have a SubkeyStateBlock in the
  // provided blob.
  size_t missing_subkeys_nodes_count_{0};
  size_t updated_subkeys_count_{0};
  size_t inserted_subkeys_count_{0};
  size_t removed_subkeys_count_{0};

  KeyStateBlock* state_block() const noexcept {
    return search_result_.state_block_;
  }

  KeyVersionBlock* version_block() const noexcept {
    return search_result_.version_block_;
  }

  void ClearSubkeyTransactions(Behavior& behavior) noexcept {
    for (auto&& [subkey, subkey_transaction] : subkey_transactions_map_)
      subkey_transaction.Reset(behavior);
    subkey_transactions_map_.clear();
  }

  // Initializes current_subkeys_count_, checks the preconditions and simplifies
  // the transaction if possible, by removing the passed preconditions and
  // unnecessary cleanups.
  bool InitializeAndValidate(KeyBlockStateSearchResult search_result) noexcept {
    search_result_ = search_result;
    current_subkeys_count_ = search_result_.GetLatestSubkeysCount();
    if (required_subkeys_count_) {
      if (*required_subkeys_count_ != current_subkeys_count_)
        return false;
      required_subkeys_count_.reset();
    }
    if (current_subkeys_count_ == 0) {
      // There are already no subkeys.
      clear_before_transaction_ = false;
    }
    return true;
  }

  size_t new_subkeys_count() const noexcept {
    assert(current_subkeys_count_ >= removed_subkeys_count_);
    return current_subkeys_count_ + missing_subkeys_nodes_count_ +
           inserted_subkeys_count_ - removed_subkeys_count_;
  }

  bool needs_new_version() const noexcept {
    return missing_subkeys_nodes_count_ + inserted_subkeys_count_ !=
           removed_subkeys_count_;
  }
};

inline bool SubkeyTransaction::InitializeAndValidate(
    Behavior& behavior,
    KeyTransaction& key_transaction) noexcept {
  const VersionedPayloadHandle latest_payload =
      search_result_.GetLatestVersionedPayload();
  if (required_payload_handle_or_deletion_marker_) {
    if (required_payload_handle_or_deletion_marker_.is_specific_handle()) {
      if (!latest_payload.has_payload() ||
          !behavior.Equal(latest_payload.payload(),
                          *required_payload_handle_or_deletion_marker_)) {
        return false;
      }
      behavior.Release(*required_payload_handle_or_deletion_marker_);
    } else {
      assert(required_payload_handle_or_deletion_marker_.is_deletion_marker());
      if (latest_payload.has_payload())
        return false;
    }
    required_payload_handle_or_deletion_marker_ = {};
  }
  if (new_payload_handle_or_deletion_marker_) {
    if (new_payload_handle_or_deletion_marker_.is_specific_handle()) {
      if (search_result_.is_state_block_found()) {
        if (latest_payload.has_payload()) {
          if (behavior.Equal(latest_payload.payload(),
                             *new_payload_handle_or_deletion_marker_)) {
            // The value is already correct.
            behavior.Release(*new_payload_handle_or_deletion_marker_);
            new_payload_handle_or_deletion_marker_ = {};
            return true;
          }
          ++key_transaction.updated_subkeys_count_;
          return true;
        }
        ++key_transaction.inserted_subkeys_count_;
        return true;
      }
      ++key_transaction.missing_subkeys_nodes_count_;
      return true;
    }
    if (latest_payload.has_payload()) {
      ++key_transaction.removed_subkeys_count_;
    } else {
      new_payload_handle_or_deletion_marker_ = {};
    }
  }
  return true;
}

struct KeyComparator {
  using is_transparent = void;
  KeyComparator(const Behavior& behavior) : behavior_{&behavior} {}

  bool operator()(KeyHandle a, KeyHandle b) const noexcept {
    return behavior_->Less(a, b);
  }

  bool operator()(const AbstractKey& a, KeyHandle b) const noexcept {
    return a.IsLessThan(b);
  }

  bool operator()(KeyHandle a, const AbstractKey& b) const noexcept {
    return b.IsGreaterThan(a);
  }

  const Behavior* behavior_;
};
using KeyTransactionsMap = std::map<KeyHandle, KeyTransaction, KeyComparator>;

class TransactionImpl : public Transaction {
 public:
  TransactionImpl(std::shared_ptr<Behavior> behavior) noexcept
      : behavior_{std::move(behavior)}, key_transactions_map_{*behavior_} {}

  ~TransactionImpl() noexcept override {
    for (auto&& [key, key_transaction] : key_transactions_map_) {
      key_transaction.ClearSubkeyTransactions(*behavior_);
      if (key_transaction.owns_key_handle_) {
        behavior_->Release(key);
        key_transaction.owns_key_handle_ = false;
      }
    }
  }

  void Put(AbstractKey& key,
           uint64_t subkey,
           PayloadHandle new_payload) noexcept override {
    SubkeyTransaction& subkey_transaction =
        GetKeyTransaction(key).subkey_transactions_map_[subkey];
    subkey_transaction.ResetNewPayload(*behavior_);
    subkey_transaction.new_payload_handle_or_deletion_marker_ = new_payload;
  }

  void Delete(AbstractKey& key, uint64_t subkey) noexcept override {
    KeyTransaction& key_transaction = GetKeyTransaction(key);
    if (key_transaction.clear_before_transaction_) {
      // In this mode we will only have the node associated with this subkey if
      // there are any other reasons to have it.
      auto& map = key_transaction.subkey_transactions_map_;
      if (auto it = map.find(subkey); it != end(map)) {
        SubkeyTransaction& subkey_transaction = it->second;
        assert(!subkey_transaction.new_payload_handle_or_deletion_marker_
                    .is_deletion_marker());
        subkey_transaction.ResetNewPayload(*behavior_);
        if (!subkey_transaction.required_payload_handle_or_deletion_marker_) {
          map.erase(it);
        }
      }
    } else {
      SubkeyTransaction& subkey_transaction =
          key_transaction.subkey_transactions_map_[subkey];
      subkey_transaction.ResetNewPayload(*behavior_);
      subkey_transaction.new_payload_handle_or_deletion_marker_ = nullptr;
    }
  }

  void ClearBeforeTransaction(AbstractKey& key) noexcept override {
    KeyTransaction& key_transaction = GetKeyTransaction(key);
    if (!key_transaction.clear_before_transaction_) {
      key_transaction.clear_before_transaction_ = true;
      auto& map = key_transaction.subkey_transactions_map_;
      for (auto it = begin(map), it_end = end(map); it != it_end;) {
        SubkeyTransaction& subkey_transaction = it->second;
        if (subkey_transaction.new_payload_handle_or_deletion_marker_
                .is_deletion_marker()) {
          subkey_transaction.new_payload_handle_or_deletion_marker_ = {};
          if (!subkey_transaction.required_payload_handle_or_deletion_marker_) {
            it = map.erase(it);
            continue;
          }
        }
        ++it;
      }
    }
  }

  void RequirePayload(AbstractKey& key,
                      uint64_t subkey,
                      PayloadHandle required_payload) noexcept override {
    SubkeyTransaction& subkey_transaction =
        GetKeyTransaction(key).subkey_transactions_map_[subkey];
    subkey_transaction.ResetRequiredPayload(*behavior_);
    subkey_transaction.required_payload_handle_or_deletion_marker_ =
        required_payload;
  }

  void RequireMissingSubkey(AbstractKey& key,
                            uint64_t subkey) noexcept override {
    SubkeyTransaction& subkey_transaction =
        GetKeyTransaction(key).subkey_transactions_map_[subkey];
    subkey_transaction.ResetRequiredPayload(*behavior_);
    subkey_transaction.required_payload_handle_or_deletion_marker_ = nullptr;
  }

  void RequireSubkeysCount(AbstractKey& key,
                           size_t required_subkeys_count) noexcept override {
    KeyTransaction& key_transaction = GetKeyTransaction(key);
    key_transaction.required_subkeys_count_.emplace(required_subkeys_count);
  }

 protected:
  PrepareResult Prepare(uint64_t new_version,
                        HeaderBlock& header_block,
                        size_t& extra_blocks_count) noexcept override {
    extra_blocks_count = 0;
    const bool is_version_added = header_block.AddVersion();
    bool allocation_failed = !is_version_added;
    HeaderBlock::Accessor accessor{header_block};
    for (auto key_transactions_it = begin(key_transactions_map_),
              key_transactions_it_end = end(key_transactions_map_);
         key_transactions_it != key_transactions_it_end;) {
      const AbstractKeyWithHandle abstract_key{
          *behavior_, key_transactions_it->first, false};
      KeyTransaction& key_transaction = key_transactions_it->second;
      SubkeyTransactionsMap& subkey_transactions =
          key_transaction.subkey_transactions_map_;

      if (!key_transaction.InitializeAndValidate(
              accessor.FindKey(abstract_key)))
        return PrepareResult::ValidationFailed;

      const bool is_key_state_found =
          key_transaction.search_result_.is_state_block_found();

      auto it = begin(subkey_transactions);
      auto it_end = end(subkey_transactions);

      if (key_transaction.clear_before_transaction_) {
        SubkeyStateBlockEnumerator enumerator =
            accessor.CreateSubkeyStateBlockEnumerator(
                key_transaction.search_result_);
        while (enumerator.MoveNext()) {
          uint64_t e_sub = enumerator.CurrentStateBlock().subkey_;
          bool already_handled = false;
          while (it != it_end && it->first <= e_sub) {
            auto& subkey_transaction = it->second;
            if (it->first == e_sub) {
              // This subkey block already exists, and it's mentioned in the
              // transaction.
              // However, if this was a validation-only node, we should still
              // attempt to delete the existing subkey.
              already_handled =
                  subkey_transaction.new_payload_handle_or_deletion_marker_;
              subkey_transaction.search_result_ = enumerator.Current();
            }
            const bool had_payload_before_validation =
                subkey_transaction.new_payload_handle_or_deletion_marker_
                    .is_specific_handle();
            // This subkey is not present in the blob.
            if (!subkey_transaction.InitializeAndValidate(*behavior_,
                                                          key_transaction)) {
              return Transaction::PrepareResult::ValidationFailed;
            }
            if (had_payload_before_validation ||
                subkey_transaction.new_payload_handle_or_deletion_marker_) {
              // One of the possible outcomes is that the transaction tried to
              // replace a payload with an already existing payload.
              // In this case, the validation will clear the subkey transaction.
              // However, we still want to keep this node here, so that it
              // avoids a cleanup (caused by the clear_before_transaction_
              // flag).
              if (!allocation_failed &&
                  subkey_transaction.new_payload_handle_or_deletion_marker_ &&
                  subkey_transaction.search_result_.is_state_block_found()) {
                allocation_failed = !accessor.ReserveSpaceForTransaction(
                    subkey_transaction.search_result_, new_version,
                    subkey_transaction.new_payload_handle_or_deletion_marker_
                        .is_specific_handle());
              }
              ++it;
            } else {
              it = subkey_transactions.erase(it);
            }
          }
          if (!already_handled) {
            // This subkey block is no mentioned in the transaction,
            // but already exists in the blob.
            if (enumerator.GetLatestPayload().has_payload()) {
              if (!allocation_failed) {
                SubkeyBlockStateSearchResult current = enumerator.Current();
                allocation_failed = !accessor.ReserveSpaceForTransaction(
                    current, new_version, false);
              }
              ++key_transaction.removed_subkeys_count_;
            }
          }
        }
      }
      // If this is a key_transaction.clear_before_transaction_ mode,
      // searching the subkey is pointless since we already iterated
      // over all existing subkeys.
      const bool should_search_subkeys =
          is_key_state_found && !key_transaction.clear_before_transaction_;

      while (it != it_end) {
        SubkeyTransaction& subkey_transaction = it->second;
        if (should_search_subkeys) {
          subkey_transaction.search_result_ =
              accessor.FindSubkey(abstract_key, it->first);
        }
        if (!subkey_transaction.InitializeAndValidate(*behavior_,
                                                      key_transaction)) {
          return Transaction::PrepareResult::ValidationFailed;
        }

        if (subkey_transaction.new_payload_handle_or_deletion_marker_) {
          if (!allocation_failed &&
              subkey_transaction.search_result_.is_state_block_found()) {
            allocation_failed = !accessor.ReserveSpaceForTransaction(
                subkey_transaction.search_result_, new_version,
                subkey_transaction.new_payload_handle_or_deletion_marker_
                    .is_specific_handle());
          }
          ++it;
        } else {
          it = subkey_transactions.erase(it);
        }
      }

      extra_blocks_count += key_transaction.missing_subkeys_nodes_count_;
      if (key_transaction.needs_new_version()) {
        if (is_key_state_found) {
          if (!allocation_failed) {
            allocation_failed = !accessor.ReserveSpaceForTransaction(
                key_transaction.search_result_);
          }
        } else {
          ++extra_blocks_count;
        }
      } else if (subkey_transactions.empty() &&
                 !key_transaction.clear_before_transaction_) {
        if (key_transaction.owns_key_handle_) {
          behavior_->Release(key_transactions_it->first);
          key_transaction.owns_key_handle_ = false;
        }
        key_transactions_it = key_transactions_map_.erase(key_transactions_it);
        continue;
      }
      ++key_transactions_it;
    }
    if (allocation_failed ||
        !header_block.CanInsertStateBlocks(extra_blocks_count)) {
      if (is_version_added) {
        // FIXME: document what's going on here.
        header_block.RemoveSnapshotReference(new_version, *behavior_);
      }
      return PrepareResult::AllocationFailed;
    }
    return PrepareResult::Ready;
  }

  void Apply(uint64_t new_version,
             HeaderBlock& header_block) noexcept override {
    HeaderBlock::Accessor accessor{header_block};

    VersionOffset version_offset =
        MakeVersionOffset(new_version, header_block.base_version());

    for (auto&& [key_handle, key_transaction] : key_transactions_map_) {
      if (!key_transaction.state_block()) {
        AbstractKeyWithHandle key{*behavior_, key_handle, true};
        // The key handle is now owned by AbstractKeyWithHandle,
        // which will then transfer the ownership to the newly inserted
        // block.
        key_transaction.owns_key_handle_ = false;
        key_transaction.search_result_ = accessor.InsertKeyBlock(key);
        assert(key_transaction.state_block());
      }
      KeyStateBlock& key_block = *key_transaction.state_block();
      if (key_transaction.needs_new_version()) {
        // FIXME: add extra validation checks that the new subkeys count is
        // representable as uint32_t (which is a limitation of the current
        // implementation).
        const uint32_t new_subkeys_count =
            static_cast<uint32_t>(key_transaction.new_subkeys_count());
        if (new_subkeys_count == 0) {
          assert(key_transaction.current_subkeys_count_ != 0);
          --header_block.keys_count();
        } else if (key_transaction.current_subkeys_count_ == 0) {
          ++header_block.keys_count();
        }
        assert(header_block.subkeys_count() >=
               key_transaction.current_subkeys_count_);
        header_block.subkeys_count() = header_block.subkeys_count() +
                                       new_subkeys_count -
                                       key_transaction.current_subkeys_count_;
        if (KeyVersionBlock* block = key_transaction.version_block()) {
          block->PushSubkeysCount(version_offset, new_subkeys_count);
        } else {
          key_block.PushSubkeysCount(version_offset, new_subkeys_count);
        }
      }

      auto& subkey_transactions = key_transaction.subkey_transactions_map_;
      auto it = begin(subkey_transactions);
      auto it_end = end(subkey_transactions);
      if (key_transaction.clear_before_transaction_) {
        SubkeyStateBlockEnumerator enumerator =
            accessor.CreateSubkeyStateBlockEnumerator(
                key_transaction.search_result_);
        while (enumerator.MoveNext()) {
          uint64_t e_sub = enumerator.CurrentStateBlock().subkey_;
          bool already_handled = false;
          while (it != it_end && it->first <= e_sub) {
            auto& subkey_transaction = it->second;
            if (it->first == e_sub) {
              // This subkey block already exists, and it's mentioned in the
              // transaction.
              already_handled = true;
            }
            if (subkey_transaction.new_payload_handle_or_deletion_marker_) {
              std::optional<PayloadHandle> new_payload =
                  subkey_transaction.new_payload_handle_or_deletion_marker_
                      .release();
              if (SubkeyVersionBlock* block =
                      subkey_transaction.version_block()) {
                block->Push(new_version, new_payload);
              } else {
                if (!subkey_transaction.state_block()) {
                  subkey_transaction.search_result_ =
                      accessor.InsertSubkeyBlock(*behavior_, key_block,
                                                 it->first);
                  assert(subkey_transaction.state_block());
                }
                subkey_transaction.state_block()->Push(new_version,
                                                       new_payload);
              }
            }
            ++it;
          }
          if (!already_handled && enumerator.GetLatestPayload().has_payload()) {
            if (enumerator.CurrentVersionBlock()) {
              enumerator.CurrentVersionBlock()->Push(new_version, {});
            } else {
              enumerator.CurrentStateBlock().Push(new_version, {});
            }
          }
        }
      }
      while (it != it_end) {
        SubkeyTransaction& subkey_transaction = it->second;
        std::optional<PayloadHandle> new_payload =
            subkey_transaction.new_payload_handle_or_deletion_marker_.release();
        if (SubkeyVersionBlock* block = subkey_transaction.version_block()) {
          block->Push(new_version, new_payload);
        } else {
          if (!subkey_transaction.state_block()) {
            subkey_transaction.search_result_ =
                accessor.InsertSubkeyBlock(*behavior_, key_block, it->first);
            assert(subkey_transaction.state_block());
          }
          subkey_transaction.state_block()->Push(new_version, new_payload);
        }
        ++it;
      }
    }
  }

  struct KeyBlockFlags {
    bool should_survive = false;
    bool is_clear_before_transaction_mode = false;
  };

  static KeyBlockFlags GetKeyBlockFlags(
      KeyStateBlockEnumerator& enumerator) noexcept {
    const KeyStateBlock& key_block = enumerator.CurrentStateBlock();
    // Blocks with subscriptions survive unconditionally because we want to
    // preserve the subscription even if there are no subkeys.
    bool should_survive = key_block.has_subscription();
    bool is_clear_before_transaction_mode = false;
    if (key_block.is_scratch_buffer_mode()) {
      // The pointer is set by the first loop of CreateMergedBlob()
      auto* pair = reinterpret_cast<KeyTransactionsMap::value_type*>(
          key_block.GetScratchBuffer());
      if (pair->second.new_subkeys_count() != 0) {
        should_survive = true;
      }
      if (pair->second.clear_before_transaction_) {
        is_clear_before_transaction_mode = true;
      }
    } else if (enumerator.GetLatestSubkeysCount() != 0) {
      should_survive = true;
    }
    return {should_survive, is_clear_before_transaction_mode};
  }

  static bool SubkeyBlockShouldSurvive(
      SubkeyStateBlock& subkey_block,
      bool is_clear_before_transaction_mode) noexcept {
    if (subkey_block.has_subscription()) {
      // Subkeys with subscriptions survive unconditionally because we want
      // to preserve the subscription even if there is no alive payload.
      return true;
    }
    if (subkey_block.is_scratch_buffer_mode()) {
      // The pointer could be saved to the scratch buffer only by the loop
      // above, so we know the type.
      auto* pair = reinterpret_cast<SubkeyTransactionsMap::value_type*>(
          subkey_block.GetScratchBuffer());
      if (pair->second.new_payload_handle_or_deletion_marker_
              .is_specific_handle()) {
        return true;
      }
      if (is_clear_before_transaction_mode &&
          !pair->second.new_payload_handle_or_deletion_marker_) {
        // This subkey exists to suppress is_clear_before_transaction_mode.
        // It was originally a Put command that was reverted because the
        // existing value was the same as the new value. Just removing the node
        // would implicitly delete this key (because of the
        // is_clear_before_transaction_mode), so that's why this placeholder is
        // here.
        return true;
      }
    } else if (!is_clear_before_transaction_mode &&
               subkey_block.GetLatestVersionedPayload().has_payload()) {
      return true;
    }
    return false;
  }

  HeaderBlock* CreateMergedBlob(
      uint64_t new_version,
      HeaderBlock& existing_header_block,
      size_t extra_state_blocks_to_insert) noexcept override {
    HeaderBlock::Accessor existing_accessor{existing_header_block};

    // Saving the pointers to map nodes to the scratch buffer area of the
    // blocks (later, when we'll be iterating over all blocks, we'll know which
    // ones have modifications).
    for (auto& key_pair : key_transactions_map_) {
      KeyTransaction& key_transaction = key_pair.second;
      if (auto* key_block = key_transaction.state_block()) {
        key_block->SetScratchBuffer(&key_pair);
        for (auto& subkey_pair : key_transaction.subkey_transactions_map_) {
          SubkeyTransaction& subkey_transaction = subkey_pair.second;
          if (auto* subkey_block = subkey_transaction.state_block()) {
            subkey_block->SetScratchBuffer(&subkey_pair);
          }
        }
      }
    }
    auto key_enumerator = existing_accessor.CreateKeyStateBlockEnumerator();

    // Counting the number of blocks that have to be allocated.
    size_t required_blocks_count = extra_state_blocks_to_insert;
    while (key_enumerator.MoveNext()) {
      KeyBlockFlags key_flags = GetKeyBlockFlags(key_enumerator);
      SubkeyStateBlockEnumerator subkey_enumerator =
          key_enumerator.CreateSubkeyStateBlockEnumerator();

      while (subkey_enumerator.MoveNext()) {
        if (SubkeyBlockShouldSurvive(
                subkey_enumerator.CurrentStateBlock(),
                key_flags.is_clear_before_transaction_mode)) {
          ++required_blocks_count;
          key_flags.should_survive = true;
        }
      }
      if (key_flags.should_survive)
        ++required_blocks_count;
    }

    HeaderBlock* new_header_block = HeaderBlock::CreateBlob(
        *behavior_, new_version, required_blocks_count * 2);

    if (!new_header_block) {
      return nullptr;
    }

    HeaderBlock::Accessor new_accessor{*new_header_block};

    // First, moving all surviving blocks from the existing blob.
    key_enumerator.Reset();

    while (key_enumerator.MoveNext()) {
      auto key_flags = GetKeyBlockFlags(key_enumerator);
      KeyStateBlock* new_key_state_block = nullptr;
      auto EnsureNewKeyBlockExists = [&] {
        if (!new_key_state_block) {
          auto& old_key_block = key_enumerator.CurrentStateBlock();
          AbstractKeyWithHandle key{
              *behavior_, key_enumerator.CurrentStateBlock().key_, false};
          KeyBlockStateSearchResult result = new_accessor.InsertKeyBlock(key);
          new_key_state_block = result.state_block_;
          assert(new_key_state_block);
          if (old_key_block.has_subscription()) {
            assert(!"Not implemented yet");
          }
          uint32_t new_subkeys_count = 0;
          if (old_key_block.is_scratch_buffer_mode()) {
            // The pointer is set by the first loop of CreateMergedBlob().
            auto* pair = reinterpret_cast<KeyTransactionsMap::value_type*>(
                old_key_block.GetScratchBuffer());

            // Replacing the old search result with the new search result.
            KeyTransaction& key_transaction = pair->second;
            key_transaction.search_result_ = result;
            // FIXME: check in the validation pass that it's not a narrowing
            // conversion.
            new_subkeys_count =
                static_cast<uint32_t>(key_transaction.new_subkeys_count());
          } else {
            new_subkeys_count = key_enumerator.GetLatestSubkeysCount();
          }
          if (new_subkeys_count) {
            new_key_state_block->PushSubkeysCount(VersionOffset{0},
                                                  new_subkeys_count);
            ++new_header_block->keys_count();
          }
        }
      };
      if (key_flags.should_survive) {
        EnsureNewKeyBlockExists();
      }
      auto subkey_enumerator =
          key_enumerator.CreateSubkeyStateBlockEnumerator();

      while (subkey_enumerator.MoveNext()) {
        SubkeyStateBlock& old_subkey_state_block =
            subkey_enumerator.CurrentStateBlock();
        if (SubkeyBlockShouldSurvive(
                old_subkey_state_block,
                key_flags.is_clear_before_transaction_mode)) {
          EnsureNewKeyBlockExists();
          const uint64_t subkey = subkey_enumerator.CurrentStateBlock().subkey_;
          SubkeyStateBlock* new_subkey_state_block =
              new_accessor
                  .InsertSubkeyBlock(*behavior_, *new_key_state_block, subkey)
                  .state_block_;
          assert(new_subkey_state_block);

          if (old_subkey_state_block.has_subscription()) {
            // Should move the subscription to the new block.
            assert(!"Not implemented yet");
          }
          bool preserve_old_payload = false;
          if (old_subkey_state_block.is_scratch_buffer_mode()) {
            // The pointer could be saved to the scratch buffer only by the loop
            // above, so we know the type.
            auto* pair = reinterpret_cast<SubkeyTransactionsMap::value_type*>(
                old_subkey_state_block.GetScratchBuffer());

            if (pair->second.new_payload_handle_or_deletion_marker_
                    .is_specific_handle()) {
              new_subkey_state_block->Push(
                  new_version,
                  pair->second.new_payload_handle_or_deletion_marker_
                      .release());
              ++new_header_block->subkeys_count();
            } else if (!pair->second.new_payload_handle_or_deletion_marker_) {
              if (key_flags.is_clear_before_transaction_mode) {
                // This subkey exists to suppress
                // is_clear_before_transaction_mode. It was originally a Put
                // command that was reverted because the existing value was the
                // same as the new value. Just removing the node would
                // implicitly delete this key (because of the
                // is_clear_before_transaction_mode), so that's why this
                // placeholder is here.
                preserve_old_payload = true;
              }
            }
          } else if (!key_flags.is_clear_before_transaction_mode) {
            preserve_old_payload = true;
          }
          if (preserve_old_payload) {
            auto old_payload = subkey_enumerator.GetLatestPayload();
            if (old_payload.has_payload()) {
              new_subkey_state_block->Push(
                  new_version,
                  behavior_->DuplicateHandle(old_payload.payload()));
              ++new_header_block->subkeys_count();
            }
          }
        }
      }
    }

    // Now handling keys and subkeys from the new transaction that were not
    // touched by the code above.
    for (auto&& [key, key_transaction] : key_transactions_map_) {
      // Can be nullptr at this point.
      // If the block was already found, the loop above would replace it with a
      // state block from the new blob.
      KeyStateBlock* new_key_state_block = key_transaction.state_block();
      auto EnsureNewKeyBlockExists = [&] {
        if (!new_key_state_block) {
          // Transferring the ownership
          assert(key_transaction.owns_key_handle_);
          key_transaction.owns_key_handle_ = false;
          AbstractKeyWithHandle abstract_key{*behavior_, key, true};
          KeyBlockStateSearchResult result =
              new_accessor.InsertKeyBlock(abstract_key);
          new_key_state_block = result.state_block_;
          if (auto count = key_transaction.new_subkeys_count()) {
            // FIXME: check in the validation pass that it's not a narrowing
            // conversion.
            new_key_state_block->PushSubkeysCount(VersionOffset{0},
                                                  static_cast<uint32_t>(count));
            ++new_header_block->keys_count();
          }
        }
      };

      for (auto&& [subkey, subkey_transaction] :
           key_transaction.subkey_transactions_map_) {
        // We are only interested in subkeys that were not handled when we were
        // traversing over all existing keys and subkeys.
        if (!subkey_transaction.state_block()) {
          assert(subkey_transaction.new_payload_handle_or_deletion_marker_
                     .is_specific_handle());
          EnsureNewKeyBlockExists();
          subkey_transaction.search_result_ = new_accessor.InsertSubkeyBlock(
              *behavior_, *new_key_state_block, subkey);

          subkey_transaction.state_block()->Push(
              new_version,
              subkey_transaction.new_payload_handle_or_deletion_marker_
                  .release());
          ++new_header_block->subkeys_count();
        }
      }
    }
    return new_header_block;
  }

  KeyTransaction& GetKeyTransaction(AbstractKey& key) noexcept {
    auto it = key_transactions_map_.lower_bound(key);
    if (it != key_transactions_map_.end() && key.IsEqualTo(it->first))
      return it->second;

    KeyHandle handle = key.MakeHandle();
    return key_transactions_map_.try_emplace(it, handle)->second;
  }

  std::shared_ptr<Behavior> behavior_;
  KeyTransactionsMap key_transactions_map_;
};  // namespace

}  // namespace

std::unique_ptr<Transaction> Transaction::Create(
    std::shared_ptr<Behavior> behavior) noexcept {
  return std::make_unique<TransactionImpl>(std::move(behavior));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
