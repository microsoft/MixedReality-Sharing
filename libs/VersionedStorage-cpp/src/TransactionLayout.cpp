// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include "src/TransactionLayout.h"

#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamReader.h>
#include <Microsoft/MixedReality/Sharing/Common/Serialization/BitstreamWriter.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

SubkeyTransactionLayout::SubkeyTransactionLayout(
    Serialization::BitstreamReader& reader) {
  bool has_action = reader.ReadBits32(1) == 0;
  if (!has_action) {
    const uint32_t prereq_kind_and_has_action = reader.ReadBits32(3);
    has_action = (prereq_kind_and_has_action & 1) == 1;
    requirement_kind_ =
        SubkeyTransactionRequirementKind{1 + (prereq_kind_and_has_action >> 1)};
    if (requirement_kind_ == SubkeyTransactionRequirementKind::ExactVersion) {
      // FIXME: use different encoding
      required_version_ = reader.ReadExponentialGolombCode();
    } else if (requirement_kind_ ==
               SubkeyTransactionRequirementKind::ExactPayload) {
      required_payload_size_ = reader.ReadExponentialGolombCode();
    }
  } else {
    requirement_kind_ = SubkeyTransactionRequirementKind::NoRequirement;
  }
  if (has_action) {
    const uint64_t action_code = reader.ReadExponentialGolombCode();
    if (action_code == 0) {
      action_kind_ = SubkeyTransactionActionKind::RemoveSubkey;
    } else {
      action_kind_ = SubkeyTransactionActionKind::PutSubkey;
      new_payload_size_ = action_code - 1;
    }
  } else {
    action_kind_ = SubkeyTransactionActionKind::NoAction;
  }
}

// FIXME: remove
void SubkeyTransactionLayout::Serialize(
    Serialization::BitstreamWriter& bitstream_writer) {
  const bool has_action = action_kind_ != SubkeyTransactionActionKind::NoAction;
  const bool has_requirement =
      requirement_kind_ != SubkeyTransactionRequirementKind::NoRequirement;
  if (has_requirement) {
    const uint64_t code =
        ((static_cast<uint64_t>(requirement_kind_) - 1) << 2) |
        (static_cast<uint64_t>(has_action) << 1) | 1ull;
    bitstream_writer.WriteBits(code, 4);
    if (requirement_kind_ == SubkeyTransactionRequirementKind::ExactVersion) {
      // FIXME: use different encoding
      bitstream_writer.WriteExponentialGolombCode(required_version_);
    } else if (requirement_kind_ ==
               SubkeyTransactionRequirementKind::ExactPayload) {
      bitstream_writer.WriteExponentialGolombCode(required_payload_size_);
    }
  } else if (has_action) {
    bitstream_writer.WriteBits(0, 1);
  } else {
    throw std::invalid_argument{
        "Can't serialize a subkey transaction that has neither actions nor "
        "requirements."};
  }
  if (has_action) {
    assert(action_kind_ != SubkeyTransactionActionKind::PutSubkey ||
           new_payload_size_ < ~0ull);
    const uint64_t code =
        action_kind_ == SubkeyTransactionActionKind::RemoveSubkey
            ? 0
            : new_payload_size_ + 1;
    bitstream_writer.WriteExponentialGolombCode(code);
  }
}

KeyTransactionLayout::KeyTransactionLayout(
    Serialization::BitstreamReader& reader)
    : key_size_{reader.ReadExponentialGolombCode()},
      subkeys_count_{reader.ReadExponentialGolombCode()} {
  auto flags = reader.ReadBits32(2);
  clear_before_transaction_ = flags & 1;
  const bool has_requirement = flags >> 1;
  if (has_requirement)
    required_subkeys_count_ = reader.ReadExponentialGolombCode();
}

void KeyTransactionLayout::Serialize(
    Serialization::BitstreamWriter& bitstream_writer) {
  bitstream_writer.WriteExponentialGolombCode(key_size_);
  bitstream_writer.WriteExponentialGolombCode(subkeys_count_);
  const bool has_requirement = required_subkeys_count_.has_value();

  uint64_t flags = static_cast<uint64_t>(clear_before_transaction_) |
                   static_cast<uint64_t>(has_requirement) << 1;
  bitstream_writer.WriteBits(flags, 2);
  if (has_requirement)
    bitstream_writer.WriteExponentialGolombCode(*required_subkeys_count_);
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
