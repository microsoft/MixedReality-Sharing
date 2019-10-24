// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Transaction.h>

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamWriter.h>
#include <Microsoft/MixedReality/Sharing/Common/Serialization/MonotonicSequenceEncoder.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptorWithHandle.h>

#include "src/HeaderBlock.h"
#include "src/StateBlock.h"
#include "src/TransactionLayout.h"

#include <map>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace {

class PayloadRequirement {
 public:
  PayloadRequirement() noexcept = default;

  PayloadRequirement(SubkeyTransactionRequirementKind kind) noexcept
      : kind_{kind} {
    assert(kind != SubkeyTransactionRequirementKind::ExactPayload &&
           kind != SubkeyTransactionRequirementKind::ExactVersion);
  }

  PayloadRequirement(PayloadHandle required_handle) noexcept
      : kind_{SubkeyTransactionRequirementKind::ExactPayload},
        required_handle_{required_handle} {}

  PayloadRequirement(uint64_t required_version) noexcept
      : kind_{SubkeyTransactionRequirementKind::ExactVersion},
        required_version_{required_version} {}

  SubkeyTransactionRequirementKind kind_{
      SubkeyTransactionRequirementKind::NoRequirement};
  union {
    PayloadHandle required_handle_;
    uint64_t required_version_;
  };
};

class PayloadOperation {
 public:
  constexpr PayloadOperation() noexcept = default;
  constexpr PayloadOperation(SubkeyTransactionActionKind kind) noexcept
      : kind_{kind} {
    assert(kind != SubkeyTransactionActionKind::PutSubkey);
  }

  constexpr PayloadOperation(PayloadHandle handle) noexcept
      : kind_{SubkeyTransactionActionKind::PutSubkey}, handle_{handle} {}

  constexpr SubkeyTransactionActionKind kind() const noexcept { return kind_; }

  SubkeyTransactionActionKind kind_{SubkeyTransactionActionKind::NoAction};
  PayloadHandle handle_{0};  // Irrelevant unless kind_ is Kind::Put
};

class SubkeyTransaction {
 public:
  void Reset(Behavior& behavior) noexcept {
    ResetRequirement(behavior);
    ResetAction(behavior);
  }

  constexpr bool has_requirement() const noexcept {
    return layout_.requirement_kind_ !=
           SubkeyTransactionRequirementKind::NoRequirement;
  }

  constexpr SubkeyTransactionActionKind action_kind() const noexcept {
    return layout_.action_kind_;
  }

  bool RequiresChange(VersionedPayloadHandle handle,
                      const Behavior& behavior) const noexcept {
    switch (layout_.action_kind_) {
      case SubkeyTransactionActionKind::RemoveSubkey:
        return handle.has_payload();
      case SubkeyTransactionActionKind::PutSubkey:
        return !handle || !behavior.Equal(new_payload_, handle.payload());
    }
    return false;
  }

  bool SatisfiesRequirements(VersionedPayloadHandle current_state,
                             const Behavior& behavior) const noexcept {
    switch (layout_.requirement_kind_) {
      case SubkeyTransactionRequirementKind::SubkeyExists:
        return current_state.has_payload();
      case SubkeyTransactionRequirementKind::SubkeyMissing:
        return !current_state.has_payload();
      case SubkeyTransactionRequirementKind::ExactVersion:
        return current_state.version() == layout_.required_version_;
      case SubkeyTransactionRequirementKind::ExactPayload:
        return current_state &&
               behavior.Equal(current_state.payload(), required_payload_);
      default:
        return true;
    }
  }

  std::optional<PayloadHandle> ReleaseHandle() noexcept {
    if (layout_.action_kind_ == SubkeyTransactionActionKind::PutSubkey) {
      layout_.action_kind_ = SubkeyTransactionActionKind::NoAction;
      return new_payload_;
    }
    return {};
  }

  void ResetAction(Behavior& behavior) noexcept {
    if (layout_.action_kind_ == SubkeyTransactionActionKind::PutSubkey)
      behavior.Release(new_payload_);
    layout_.action_kind_ = SubkeyTransactionActionKind::NoAction;
  }

  void SetActionPut(PayloadHandle new_payload, Behavior& behavior) noexcept {
    if (layout_.action_kind_ == SubkeyTransactionActionKind::PutSubkey)
      behavior.Release(new_payload_);
    layout_.action_kind_ = SubkeyTransactionActionKind::PutSubkey;
    // Note: the size in the layout is not initialized here, it will be set
    // after the serialization of the payload.
    new_payload_ = new_payload;
  }

  void SetActionRemove(Behavior& behavior) noexcept {
    if (layout_.action_kind_ == SubkeyTransactionActionKind::PutSubkey)
      behavior.Release(new_payload_);
    layout_.action_kind_ = SubkeyTransactionActionKind::RemoveSubkey;
  }

  void ResetRequirement(
      Behavior& behavior,
      SubkeyTransactionRequirementKind new_requirement =
          SubkeyTransactionRequirementKind::NoRequirement) noexcept {
    if (layout_.requirement_kind_ ==
        SubkeyTransactionRequirementKind::ExactPayload)
      behavior.Release(required_payload_);
    layout_.requirement_kind_ = new_requirement;
  }

  void SetRequiredPayload(Behavior& behavior,
                          PayloadHandle required_payload) noexcept {
    ResetRequirement(behavior, SubkeyTransactionRequirementKind::ExactPayload);
    // Note: the size in the layout is not initialized here, it will be set
    // after the serialization of the payload.
    required_payload_ = required_payload;
  }

  void SetRequiredVersion(Behavior& behavior,
                          uint64_t required_version) noexcept {
    ResetRequirement(behavior, SubkeyTransactionRequirementKind::ExactVersion);
    layout_.required_version_ = required_version;
  }

  void Serialize(Serialization::BitstreamWriter& bitstream_writer,
                 std::vector<std::byte>& byte_stream,
                 Behavior& behavior) noexcept {
    if (layout_.requirement_kind_ ==
        SubkeyTransactionRequirementKind::ExactPayload) {
      layout_.required_payload_size_ =
          behavior.Serialize(required_payload_, byte_stream);
    }
    if (layout_.action_kind_ == SubkeyTransactionActionKind::PutSubkey) {
      layout_.new_payload_size_ = behavior.Serialize(new_payload_, byte_stream);
    }
    layout_.Serialize(bitstream_writer);
  }

 private:
  SubkeyTransactionLayout layout_;
  PayloadHandle required_payload_{0};
  PayloadHandle new_payload_{0};
};

using SubkeyTransactionsMap = std::map<uint64_t, SubkeyTransaction>;

struct KeyTransaction {
  ~KeyTransaction() noexcept { assert(owns_key_handle_ == false); }

  void ClearSubkeyTransactions(Behavior& behavior) noexcept {
    for (auto&& [subkey, subkey_transaction] : subkeys_)
      subkey_transaction.Reset(behavior);
    subkeys_.clear();
  }

  KeyTransactionLayout layout_;
  bool owns_key_handle_{true};

  SubkeyTransactionsMap subkeys_;

  uint32_t current_subkeys_count_{0};

  // Number of inserted subkeys that didn't have a SubkeyStateBlock in the
  // provided blob.
  size_t missing_subkeys_nodes_count_{0};
  size_t updated_subkeys_count_{0};
  size_t inserted_subkeys_count_{0};
  size_t removed_subkeys_count_{0};
};

struct KeyComparator {
  using is_transparent = void;
  KeyComparator(const Behavior& behavior) : behavior_{&behavior} {}

  bool operator()(KeyHandle a, KeyHandle b) const noexcept {
    return behavior_->Less(a, b);
  }

  bool operator()(const KeyDescriptor& a, KeyHandle b) const noexcept {
    return a.IsLessThan(b);
  }

  bool operator()(KeyHandle a, const KeyDescriptor& b) const noexcept {
    return b.IsGreaterThan(a);
  }

  const Behavior* behavior_;
};

using KeyTransactionsMap = std::map<KeyHandle, KeyTransaction, KeyComparator>;

class TransactionImpl : public TransactionBuilder {
 public:
  uint64_t mentioned_keys_count() const noexcept override {
    return key_transactions_map_.size();
  }

  uint64_t mentioned_subkeys_count_hint() const noexcept override {
    return mentioned_subkeys_count_;
  }

  bool MoveNextKey() noexcept override {
    // FIXME: temporary code
    if (is_iterating_over_keys_) {
      if (key_it_ == key_transactions_map_.end())
        return false;
      ++key_it_;
      if (key_it_ == key_transactions_map_.end())
        return false;
    } else {
      if (key_transactions_map_.empty())
        return false;
      key_it_ = key_transactions_map_.begin();
      is_iterating_over_keys_ = true;
    }
    is_iterating_over_subkeys_ = false;
    return true;
  }

  bool MoveNextSubkey() noexcept override {
    // FIXME: temporary code
    if (is_iterating_over_subkeys_) {
      if (subkey_it_ == key_it_->second.subkeys_.end())
        return false;
      ++subkey_it_;
      if (subkey_it_ == key_it_->second.subkeys_.end())
        return false;
    } else {
      if (key_it_->second.subkeys_.empty())
        return false;
      subkey_it_ = key_it_->second.subkeys_.begin();
      is_iterating_over_subkeys_ = true;
    }
    current_subkey_ = subkey_it_->first;
    return true;
  }

  KeyTransactionView GetKeyTransactionView() noexcept override {
    assert(is_iterating_over_keys_);
    assert(key_it_ != key_transactions_map_.end());
    key_descriptor_.ReplaceHandle(key_it_->first,
                                  key_it_->second.owns_key_handle_);
    // FIXME: do something about the ownership issue
    key_it_->second.owns_key_handle_ = false;
    return {key_descriptor_, key_it_->second.layout_.clear_before_transaction_,
            key_it_->second.layout_.required_subkeys_count_};
  }

  SubkeyTransactionView GetSubkeyTransactionView(
      VersionedPayloadHandle current_state) noexcept override {
    assert(is_iterating_over_subkeys_);
    assert(subkey_it_ != key_it_->second.subkeys_.end());
    SubkeyTransaction& tx = subkey_it_->second;

    if (!tx.SatisfiesRequirements(current_state, *behavior_))
      return {SubkeyTransactionView::Operation::ValidationFailed};

    if (!tx.RequiresChange(current_state, *behavior_))
      return {SubkeyTransactionView::Operation::NoChangeRequired};

    if (auto handle = tx.ReleaseHandle())
      return {*handle};

    return {SubkeyTransactionView::Operation::RemoveSubkey};
  }

  TransactionImpl(std::shared_ptr<Behavior> behavior) noexcept
      : behavior_{std::move(behavior)},
        key_transactions_map_{*behavior_},
        key_descriptor_{*behavior_, KeyHandle{0}, false} {}

  ~TransactionImpl() noexcept override {
    for (auto&& [key, key_transaction] : key_transactions_map_) {
      key_transaction.ClearSubkeyTransactions(*behavior_);
      if (key_transaction.owns_key_handle_) {
        behavior_->Release(key);
        key_transaction.owns_key_handle_ = false;
      }
    }
  }

  void Put(KeyDescriptor& key,
           uint64_t subkey,
           PayloadHandle new_payload) noexcept override {
    SubkeyTransaction& subkey_transaction = GetSubkeyTransaction(key, subkey);
    subkey_transaction.SetActionPut(new_payload, *behavior_);
  }

  void Delete(KeyDescriptor& key, uint64_t subkey) noexcept override {
    KeyTransaction& key_transaction = GetKeyTransaction(key);
    if (key_transaction.layout_.clear_before_transaction_) {
      // In this mode we will only have the node associated with this subkey if
      // there are any other reasons to have it.
      auto& map = key_transaction.subkeys_;
      if (auto it = map.find(subkey); it != end(map)) {
        SubkeyTransaction& tx = it->second;
        tx.ResetAction(*behavior_);
        if (!tx.has_requirement()) {
          map.erase(it);
          --mentioned_subkeys_count_;
        }
      }
    } else {
      SubkeyTransaction& tx = GetSubkeyTransaction(key, subkey);
      tx.SetActionRemove(*behavior_);
    }
  }

  void ClearBeforeTransaction(KeyDescriptor& key) noexcept override {
    KeyTransaction& key_transaction = GetKeyTransaction(key);
    if (!key_transaction.layout_.clear_before_transaction_) {
      key_transaction.layout_.clear_before_transaction_ = true;
      auto& map = key_transaction.subkeys_;
      for (auto it = begin(map), it_end = end(map); it != it_end;) {
        SubkeyTransaction& subkey_transaction = it->second;
        if (subkey_transaction.action_kind() ==
            SubkeyTransactionActionKind::RemoveSubkey) {
          subkey_transaction.ResetAction(*behavior_);
          if (!subkey_transaction.has_requirement()) {
            it = map.erase(it);
            --mentioned_subkeys_count_;
            continue;
          }
        }
        ++it;
      }
    }
  }

  void RequirePresentSubkey(KeyDescriptor& key,
                            uint64_t subkey) noexcept override {
    SubkeyTransaction& tx = GetSubkeyTransaction(key, subkey);
    tx.ResetRequirement(*behavior_,
                        SubkeyTransactionRequirementKind::SubkeyExists);
  }

  void RequireMissingSubkey(KeyDescriptor& key,
                            uint64_t subkey) noexcept override {
    SubkeyTransaction& tx = GetSubkeyTransaction(key, subkey);
    tx.ResetRequirement(*behavior_,
                        SubkeyTransactionRequirementKind::SubkeyMissing);
  }

  void RequireExactPayload(KeyDescriptor& key,
                           uint64_t subkey,
                           PayloadHandle required_payload) noexcept override {
    SubkeyTransaction& tx = GetSubkeyTransaction(key, subkey);
    tx.SetRequiredPayload(*behavior_, required_payload);
  }

  void RequireExactVersion(KeyDescriptor& key,
                           uint64_t subkey,
                           uint64_t required_version) noexcept override {
    SubkeyTransaction& tx = GetSubkeyTransaction(key, subkey);
    tx.SetRequiredVersion(*behavior_, required_version);
  }

  void RequireSubkeysCount(KeyDescriptor& key,
                           size_t required_subkeys_count) noexcept override {
    KeyTransaction& key_transaction = GetKeyTransaction(key);
    key_transaction.layout_.required_subkeys_count_.emplace(
        required_subkeys_count);
  }

  void Serialize(Serialization::BitstreamWriter& bitstream_writer,
                 std::vector<std::byte>& byte_stream) noexcept override {
    bitstream_writer.WriteExponentialGolombCode(key_transactions_map_.size());
    for (auto&& [key, key_transaction] : key_transactions_map_) {
      key_transaction.layout_.key_size_ =
          behavior_->Serialize(key, byte_stream);
      key_transaction.layout_.subkeys_count_ = key_transaction.subkeys_.size();
      key_transaction.layout_.Serialize(bitstream_writer);
      Serialization::MonotonicSequenceEncoder subkey_encoder;
      for (auto&& [subkey, subkey_transaction] : key_transaction.subkeys_) {
        subkey_encoder.EncodeNext(subkey, bitstream_writer);
        subkey_transaction.Serialize(bitstream_writer, byte_stream, *behavior_);
      }
    }
  }

 protected:
  KeyTransaction& GetKeyTransaction(KeyDescriptor& key) noexcept {
    auto it = key_transactions_map_.lower_bound(key);
    if (it != key_transactions_map_.end() && key.IsEqualTo(it->first))
      return it->second;

    KeyHandle handle = key.MakeHandle();
    return key_transactions_map_.try_emplace(it, handle)->second;
  }

  SubkeyTransaction& GetSubkeyTransaction(KeyDescriptor& key, uint64_t subkey) {
    auto pair = GetKeyTransaction(key).subkeys_.try_emplace(subkey);
    if (pair.second) {
      ++mentioned_subkeys_count_;
    }
    return pair.first->second;
  }

  std::shared_ptr<Behavior> behavior_;
  KeyTransactionsMap key_transactions_map_;
  KeyDescriptorWithHandle key_descriptor_;
  bool is_iterating_over_keys_ = false;
  bool is_iterating_over_subkeys_ = false;
  KeyTransactionsMap::iterator key_it_;
  SubkeyTransactionsMap::iterator subkey_it_;

  size_t mentioned_subkeys_count_{0};
};

}  // namespace

std::unique_ptr<TransactionBuilder> TransactionBuilder::Create(
    std::shared_ptr<Behavior> behavior) noexcept {
  return std::make_unique<TransactionImpl>(std::move(behavior));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
