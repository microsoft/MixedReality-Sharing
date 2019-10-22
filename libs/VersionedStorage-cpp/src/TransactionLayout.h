// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

namespace Microsoft::MixedReality::Sharing::Serialization {
class BitstreamReader;
class BitstreamWriter;
}  // namespace Microsoft::MixedReality::Sharing::Serialization

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

enum class SubkeyTransactionRequirementKind {
  NoRequirement,
  SubkeyExists,
  SubkeyMissing,
  ExactVersion,
  ExactPayload,
};

enum class SubkeyTransactionActionKind {
  NoAction,
  RemoveSubkey,
  PutSubkey,
};

struct SubkeyTransactionLayout {
  // Note: default-constructed layout is not valid and can't be serialized
  // (a valid subkey transaction must have an action, a requirement, or both).
  SubkeyTransactionLayout() = default;
  SubkeyTransactionLayout(Serialization::BitstreamReader& reader);
  void Serialize(Serialization::BitstreamWriter& writer);

  constexpr uint64_t bytestream_content_size() const noexcept {
    uint64_t result = action_kind_ == SubkeyTransactionActionKind::PutSubkey
                          ? new_payload_size_
                          : 0;
    if (requirement_kind_ == SubkeyTransactionRequirementKind::ExactPayload)
      result += required_payload_size_;
    return result;
  }

  SubkeyTransactionRequirementKind requirement_kind_{
      SubkeyTransactionRequirementKind::NoRequirement};

  union {
    uint64_t required_payload_size_;
    uint64_t required_version_;
  };
  SubkeyTransactionActionKind action_kind_{
      SubkeyTransactionActionKind::NoAction};
  uint64_t new_payload_size_{0};
};

struct KeyTransactionLayout {
  // Note: default-constructed layout is not valid and can't be serialized
  // (a valid key transaction must mention subkeys, have requirements or clear
  // the key).
  KeyTransactionLayout() = default;
  KeyTransactionLayout(Serialization::BitstreamReader& reader);
  void Serialize(Serialization::BitstreamWriter& writer);

  uint64_t key_size_{0};
  uint64_t subkeys_count_{0};
  bool clear_before_transaction_{false};
  std::optional<uint64_t> required_subkeys_count_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
