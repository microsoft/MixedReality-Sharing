// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <cstdint>
#include <string_view>

namespace Microsoft::MixedReality::Sharing {

uint64_t CalculateHash64(const char* data,
                         size_t size,
                         uint64_t seed = 0) noexcept;

uint64_t CalculateHash64(uint64_t value_a, uint64_t value_b) noexcept;

inline uint64_t CalculateHash64(std::string_view sv) noexcept {
  return CalculateHash64(sv.data(), sv.size());
}

}  // namespace Microsoft::MixedReality::Sharing
