// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include "src/KeyVersionBlock.h"
#include "src/StateBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

struct KeyStateView {
  KeyStateView() noexcept = default;
  KeyStateView(IndexBlockSlot* index_block_slot,
               KeyStateBlock* state_block,
               KeyVersionBlock* version_block) noexcept
      : index_block_slot_{index_block_slot},
        state_block_{state_block},
        version_block_{version_block} {}

  constexpr explicit operator bool() const noexcept { return state_block_; }

  constexpr KeyHandle key() const noexcept { return state_block_->key_; }

  uint32_t GetSubkeysCount(VersionOffset offset) const noexcept {
    if (version_block_)
      return version_block_->GetSubkeysCount(offset);
    if (state_block_)
      return state_block_->GetSubkeysCount(offset);
    return 0;
  }

  uint32_t latest_subkeys_count_thread_unsafe() const noexcept {
    if (version_block_)
      return version_block_->latest_subkeys_count_thread_unsafe();
    if (state_block_)
      return state_block_->latest_subkeys_count_thread_unsafe();
    return 0;
  }

  IndexBlockSlot* index_block_slot_{nullptr};
  KeyStateBlock* state_block_{nullptr};
  KeyVersionBlock* version_block_{nullptr};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
