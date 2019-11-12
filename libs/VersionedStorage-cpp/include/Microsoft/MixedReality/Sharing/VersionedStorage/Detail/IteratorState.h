// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/BlobLayout.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/layout.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyView.h>

#include <cstdint>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

class KeyStateBlock;
class SubkeyStateBlock;

// Exposed for PInvoke, matches the layout of KeyIteratorState the C# side.
struct KeyIteratorState {
  constexpr KeyIteratorState() = default;
  BlobLayout blob_layout_;
  uint64_t version_{0};
  KeyStateBlock* current_state_block_{nullptr};
};

// Exposed for PInvoke, matches the layout of SubkeyIteratorState the C# side.
struct SubkeyIteratorState {
  constexpr SubkeyIteratorState() = default;
  BlobLayout blob_layout_;
  uint64_t version_{0};
  SubkeyStateBlock* current_state_block_{nullptr};

  SubkeyView AdvanceUntilPayloadFound(IndexSlotLocation next_location) noexcept;
  SubkeyView AdvanceUntilPayloadFound() noexcept;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
