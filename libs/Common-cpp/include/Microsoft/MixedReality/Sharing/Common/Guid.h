// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <cstdint>

namespace Microsoft::MixedReality::Sharing {

struct Guid {
  uint64_t data[2];
};

inline bool operator==(const Guid& a, const Guid& b) noexcept {
  return a.data[0] == b.data[0] && a.data[1] == b.data[1];
}

inline bool operator!=(const Guid& a, const Guid& b) noexcept {
  return a.data[0] != b.data[0] || a.data[1] != b.data[1];
}

}  // namespace Microsoft::MixedReality::Sharing
