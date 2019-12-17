// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyIterator.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>

#include "src/HeaderBlock.h"
#include "src/IndexBlock.h"
#include "src/StateBlock.h"
#include "src/SubkeyVersionBlock.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace Detail {

MS_MR_SHARING_FORCEINLINE
SubkeyView SubkeyIteratorState::AdvanceUntilPayloadFound(
    IndexSlotLocation next_location) noexcept {
  while (next_location != IndexSlotLocation::kInvalid) {
    IndexBlockSlot& index_block_slot =
        IndexBlock::GetSlot(blob_layout_.index_begin_, next_location);

    current_state_block_ = &GetBlockAt<SubkeyStateBlock>(
        blob_layout_.data_begin_, index_block_slot.state_block_location_);

    const DataBlockLocation version_block_location =
        index_block_slot.version_block_location_.load(
            std::memory_order_acquire);

    if (version_block_location != DataBlockLocation::kInvalid) {
      Platform::Prefetch(current_state_block_);
      SubkeyVersionBlock& version_block = GetBlockAt<SubkeyVersionBlock>(
          blob_layout_.data_begin_, version_block_location);

      if (VersionedPayloadHandle handle =
              version_block.GetVersionedPayload(version_)) {
        return {current_state_block_->subkey_, handle};
      }
    } else if (VersionedPayloadHandle handle =
                   current_state_block_->GetVersionedPayload(version_)) {
      return {current_state_block_->subkey_, handle};
    }

    next_location = current_state_block_->next_.load(std::memory_order_acquire);
  }
  current_state_block_ = nullptr;
  return {};
}

SubkeyView SubkeyIteratorState::AdvanceUntilPayloadFound() noexcept {
  return AdvanceUntilPayloadFound(
      current_state_block_->next_.load(std::memory_order_acquire));
}

}  // namespace Detail

SubkeyIterator::SubkeyIterator(const KeyView& key_view,
                               const Snapshot& snapshot) noexcept {
  assert(snapshot.header_block_);
  if (key_view.subkeys_count()) {
    state_.version_ = snapshot.info_.version_;
    Detail::BlobAccessor accessor(*snapshot.header_block_);
    state_.blob_layout_ = accessor.blob_layout_;
    auto* key_state_block =
        static_cast<Detail::KeyStateBlock*>(key_view.key_handle_wrapper_);
    current_subkey_view_ = state_.AdvanceUntilPayloadFound(
        key_state_block->subkeys_list_head_.load(std::memory_order_acquire));
  }
}

void SubkeyIterator::Advance() noexcept {
  assert(!is_end());
  // Could call the single-arg version, but we want more aggressive inlining
  // here.
  current_subkey_view_ = state_.AdvanceUntilPayloadFound(
      state_.current_state_block_->next_.load(std::memory_order_acquire));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
