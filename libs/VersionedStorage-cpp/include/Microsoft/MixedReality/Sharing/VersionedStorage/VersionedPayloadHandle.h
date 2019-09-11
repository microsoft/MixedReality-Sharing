// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <cassert>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// A PayloadHandle object with a version that indicates when this payload value
// has been assigned.
// Can act as std::optional (invalid version indicates the absence of
// the payload), see Snapshot::Get() for an example.
class VersionedPayloadHandle {
 public:
  constexpr VersionedPayloadHandle() noexcept = default;
  VersionedPayloadHandle(uint64_t version, PayloadHandle payload) noexcept
      : version_{version}, payload_{payload} {
    assert(version < kInvalidVersion);
  }

  constexpr bool has_payload() const noexcept {
    return version_ < kInvalidVersion;
  }

  explicit constexpr operator bool() const noexcept {
    return version_ < kInvalidVersion;
  }

  // Returns kInvalidVersion
  uint64_t version() const noexcept { return version_; }

  // VersionedPayloadHandle objects returned by methods that can fail to find a
  // payload (such as Snapshot::Get()) must call
  // has_payload() or operator bool() first to check that the payload exists.
  PayloadHandle payload() const noexcept {
    assert(has_payload());
    return payload_;
  }

  constexpr bool operator==(const VersionedPayloadHandle& other) const
      noexcept {
    return version_ == other.version_ && payload_ == other.payload_;
  }

  constexpr bool operator!=(const VersionedPayloadHandle& other) const
      noexcept {
    return version_ != other.version_ || payload_ != other.payload_;
  }

 private:
  uint64_t version_{kInvalidVersion};

  // All invalid VersionedPayloadHandle objects will always have the same
  // payload_ value to avoid branching in operator==.
  PayloadHandle payload_{0};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
