// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class SubkeyView {
 public:
  constexpr SubkeyView() noexcept = default;
  constexpr SubkeyView(uint64_t subkey,
                       uint64_t version,
                       PayloadHandle payload) noexcept
      : subkey_{subkey}, version_{version}, payload_{payload} {}

  constexpr uint64_t subkey() const noexcept { return subkey_; }

  // The version of the storage when this specific payload handle was set as the
  // current value for this subkey. For any snapshot, this field is going to be
  // less than or equal to the version of the snapshot.
  // If for two different snapshots the version of some subkey is identical,
  // it's safe to assume that the payload is also identical.
  constexpr uint64_t version() const noexcept { return version_; }

  // Non-owning view (will be valid for as long as the snapshot is alive).
  constexpr PayloadHandle payload() const noexcept { return payload_; }

 private:
  uint64_t subkey_{0};
  uint64_t version_{Detail::kSmallestInvalidVersion};
  PayloadHandle payload_{0};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
