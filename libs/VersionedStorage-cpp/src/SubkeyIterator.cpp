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

MS_MR_SHARING_FORCEINLINE
void SubkeyIterator::AdvanceUntilPayloadFound(
    Detail::IndexSlotLocation location) noexcept {
  while (location != Detail::IndexSlotLocation::kInvalid) {
    Detail::IndexBlockSlot& index_block_slot =
        Detail::IndexBlock::GetSlot(index_begin_, location);

    current_state_block_ = &Detail::GetBlockAt<Detail::SubkeyStateBlock>(
        data_begin_, index_block_slot.state_block_location_);

    const Detail::DataBlockLocation version_block_location =
        index_block_slot.version_block_location_.load(
            std::memory_order_acquire);

    if (version_block_location != Detail::DataBlockLocation::kInvalid) {
      Platform::Prefetch(current_state_block_);
      Detail::SubkeyVersionBlock& version_block =
          Detail::GetBlockAt<Detail::SubkeyVersionBlock>(
              data_begin_, version_block_location);

      if (VersionedPayloadHandle handle =
              version_block.GetVersionedPayload(version_)) {
        current_subkey_view_ = {current_state_block_->subkey_, handle};
        return;
      }
    } else if (VersionedPayloadHandle handle =
                   current_state_block_->GetVersionedPayload(version_)) {
      current_subkey_view_ = {current_state_block_->subkey_, handle};
      return;
    }

    location = current_state_block_->next_.load(std::memory_order_acquire);
  }
  current_state_block_ = nullptr;
}

SubkeyIterator::SubkeyIterator(const KeyView& key_view,
                               const Snapshot& snapshot) noexcept {
  assert(snapshot.header_block_);
  if (key_view.subkeys_count()) {
    version_ = snapshot.version_;
    Detail::BlobAccessor accessor(*snapshot.header_block_);
    index_begin_ = accessor.index_begin_;
    data_begin_ = accessor.data_begin_;
    auto* key_state_block =
        static_cast<Detail::KeyStateBlock*>(key_view.key_handle_wrapper_);
    AdvanceUntilPayloadFound(
        key_state_block->subkeys_list_head_.load(std::memory_order_acquire));
  }
}

void SubkeyIterator::Advance() noexcept {
  assert(!is_end());
  AdvanceUntilPayloadFound(
      current_state_block_->next_.load(std::memory_order_acquire));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
