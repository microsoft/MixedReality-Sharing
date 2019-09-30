// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include "src/StateBlock.h"
#include "src/SubkeyVersionBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

struct SubkeyStateView {
  constexpr SubkeyStateView() noexcept = default;
  constexpr SubkeyStateView(SubkeyStateBlock* state_block,
                            SubkeyVersionBlock* version_block) noexcept
      : state_block_{state_block}, version_block_{version_block} {}

  // Ignores the last argument
  constexpr SubkeyStateView(SubkeyStateBlock* state_block,
                            SubkeyVersionBlock* version_block,
                            IndexBlockSlot*) noexcept
      : state_block_{state_block}, version_block_{version_block} {}

  constexpr explicit operator bool() const noexcept { return state_block_; }

  constexpr KeyHandle key() const noexcept { return state_block_->key_; }
  constexpr uint64_t subkey() const noexcept { return state_block_->subkey_; }

  VersionedPayloadHandle GetPayload(uint64_t version) const noexcept {
    if (version_block_)
      return version_block_->GetVersionedPayload(version);
    if (state_block_)
      return state_block_->GetVersionedPayload(version);
    return {};
  }

  VersionedPayloadHandle latest_payload_thread_unsafe() const noexcept {
    if (version_block_)
      return version_block_->latest_versioned_payload_thread_unsafe();
    if (state_block_)
      return state_block_->latest_versioned_payload_thread_unsafe();
    return {};
  }

  SubkeyStateBlock* state_block_{nullptr};
  SubkeyVersionBlock* version_block_{nullptr};
};

struct SubkeyStateAndIndexView : public SubkeyStateView {
  constexpr SubkeyStateAndIndexView() = default;
  constexpr SubkeyStateAndIndexView(SubkeyStateBlock* state_block,
                                    SubkeyVersionBlock* version_block,
                                    IndexBlockSlot* index_block_slot) noexcept
      : SubkeyStateView{state_block, version_block},
        index_block_slot_{index_block_slot} {}

  IndexBlockSlot* index_block_slot_{nullptr};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
