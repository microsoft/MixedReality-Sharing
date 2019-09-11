// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

// References a slots in the index section of the blob
// (each block has several slots).
enum class IndexSlotLocation : uint32_t { kInvalid = ~0u };

// References blocks in the data section
// (both the state blocks and the version blocks).
enum class DataBlockLocation : uint32_t { kInvalid = ~0u };

// An small offset from some base version.
enum class VersionOffset : uint32_t { kInvalid = ~0u };

// Forward declarations

class HeaderBlock;
class IndexBlock;
class KeyStateBlock;
class SubkeyStateBlock;

struct KeyHandleWrapper {
  const KeyHandle key_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
