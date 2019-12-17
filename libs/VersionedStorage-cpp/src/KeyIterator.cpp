// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyIterator.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>

#include "src/HeaderBlock.h"
#include "src/IndexBlock.h"
#include "src/KeyVersionBlock.h"
#include "src/StateBlock.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

MS_MR_SHARING_FORCEINLINE
void KeyIterator::AdvanceUntilSubkeysFound(
    Detail::IndexSlotLocation location) noexcept {
  while (location != Detail::IndexSlotLocation::kInvalid) {
    Detail::IndexBlockSlot& index_block_slot =
        Detail::IndexBlock::GetSlot(blob_layout_.index_begin_, location);
    auto& key_state_block = Detail::GetBlockAt<Detail::KeyStateBlock>(
        blob_layout_.data_begin_, index_block_slot.state_block_location_);
    const Detail::DataBlockLocation version_block_location =
        index_block_slot.version_block_location_.load(
            std::memory_order_acquire);

    if (version_block_location != Detail::DataBlockLocation::kInvalid) {
      Platform::Prefetch(&key_state_block);
      Detail::KeyVersionBlock& version_block =
          Detail::GetBlockAt<Detail::KeyVersionBlock>(blob_layout_.data_begin_,
                                                      version_block_location);
      if (auto count = version_block.GetSubkeysCount(version_offset_)) {
        current_key_view_ = {count, &key_state_block};
        return;
      }
    } else if (auto count = key_state_block.GetSubkeysCount(version_offset_)) {
      current_key_view_ = {count, &key_state_block};
      return;
    }
    location = key_state_block.next_.load(std::memory_order_acquire);
  }
  current_key_view_ = {};
}

KeyIterator::KeyIterator(const Snapshot& snapshot) noexcept {
  if (snapshot.header_block_) {
    Detail::BlobAccessor accessor(*snapshot.header_block_);
    version_offset_ = Detail::MakeVersionOffset(
        snapshot.info_.version_, snapshot.header_block_->base_version());
    blob_layout_ = accessor.blob_layout_;
    AdvanceUntilSubkeysFound(snapshot.header_block_->keys_list_head_acquire());
  }
}

void KeyIterator::Advance() noexcept {
  assert(!is_end());
  auto* key_state_block = static_cast<Detail::KeyStateBlock*>(
      current_key_view_.key_handle_wrapper_);
  AdvanceUntilSubkeysFound(
      key_state_block->next_.load(std::memory_order_acquire));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
