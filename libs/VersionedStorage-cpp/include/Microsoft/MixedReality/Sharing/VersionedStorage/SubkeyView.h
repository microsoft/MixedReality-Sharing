// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/VersionedPayloadHandle.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class SubkeyView {
 public:
  constexpr SubkeyView() noexcept = default;
  constexpr SubkeyView(uint64_t subkey,
                       VersionedPayloadHandle versioned_payload_handle) noexcept
      : subkey_{subkey}, versioned_payload_handle_{versioned_payload_handle} {}

  constexpr uint64_t subkey() const noexcept { return subkey_; }

  // The version of the storage when this specific payload handle was set as the
  // current value for this subkey. For any snapshot, this field is going to be
  // less than or equal to the version of the snapshot.
  // If for two different snapshots the versions of some subkey are identical,
  // the payloads are also guaranteed to be identical, in a sense defined by
  // the Behavior::Equal(). Note that the binary equality of the handles (as
  // enums) is only guaranteed if Behavior::DuplicateHandle() gurantees the
  // same.
  uint64_t version() const noexcept {
    return versioned_payload_handle_.version();
  }

  // Non-owning view (will be valid for as long as the snapshot is alive).
  PayloadHandle payload() const noexcept {
    return versioned_payload_handle_.payload();
  }

  constexpr VersionedPayloadHandle versioned_payload() const noexcept {
    return versioned_payload_handle_;
  }

  // SubkeyView obtained by iterating over a key always have payloads, so
  // calling this method is unnecessary for that case.
  constexpr operator bool() const noexcept {
    return versioned_payload_handle_.has_payload();
  }

 private:
  uint64_t subkey_{0};
  VersionedPayloadHandle versioned_payload_handle_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
