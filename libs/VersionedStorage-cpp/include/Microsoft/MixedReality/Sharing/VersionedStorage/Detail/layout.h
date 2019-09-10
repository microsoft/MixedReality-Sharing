// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <cstdint>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

// References a slots in the index section of the blob
// (each block has several slots).
enum class IndexSlotLocation : uint32_t { kInvalid = ~0u };

// References blocks in the data section
// (both the state blocks and the version blocks).
enum class DataBlockLocation : uint32_t { kInvalid = ~0u };

// An small offset from some base version.
enum class VersionOffset : uint32_t { kInvalid = ~0u };

// Versions greater or equal to this value are considered to be invalid.
static constexpr uint64_t kSmallestInvalidVersion = 0x7FFF'FFFF'FFFF'FFFF;

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
