// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyView.h>

#include "src/HeaderBlock.h"
#include "src/IndexBlock.h"
#include "src/KeyVersionBlock.h"
#include "src/StateBlock.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

MS_MR_SHARING_FORCEINLINE
void KeyView::AdvanceUntilSubkeysFound(
    Detail::IndexSlotLocation location,
    Detail::VersionOffset version_offset) noexcept {
  while (location != Detail::IndexSlotLocation::kInvalid) {
    Detail::IndexBlockSlot& index_block_slot =
        Detail::IndexBlock::GetSlot(index_begin_, location);

    key_state_block_ = &Detail::GetBlockAt<Detail::KeyStateBlock>(
        data_begin_, index_block_slot.state_block_location_);
    const Detail::DataBlockLocation version_block_location =
        index_block_slot.version_block_location_.load(
            std::memory_order_acquire);

    if (version_block_location != Detail::DataBlockLocation::kInvalid) {
      Platform::Prefetch(key_state_block_);
      Detail::KeyVersionBlock& version_block =
          Detail::GetBlockAt<Detail::KeyVersionBlock>(data_begin_,
                                                      version_block_location);

      if (auto count = version_block.GetSubkeysCount(version_offset)) {
        subkeys_count_ = count;
        return;
      }
    } else if (auto count = key_state_block_->GetSubkeysCount(version_offset)) {
      subkeys_count_ = count;
      return;
    }
    location = key_state_block_->next_.load(std::memory_order_acquire);
  }
  subkeys_count_ = 0;
  key_state_block_ = nullptr;
}

KeyView::KeyView(uint64_t observed_version,
                 Detail::VersionOffset version_offset,
                 Detail::HeaderBlock& header_block) noexcept
    : observed_version_{observed_version} {
  Detail::BlobAccessor accessor(header_block);
  index_begin_ = accessor.index_begin_;
  data_begin_ = accessor.data_begin_;
  AdvanceUntilSubkeysFound(header_block.keys_list_head_acquire(),
                           version_offset);
}

KeyHandle KeyView::key_handle() const noexcept {
  assert(key_state_block_);
  return key_state_block_->key_;
}

SubkeyIterator KeyView::begin() const noexcept {
  return {observed_version_,
          key_state_block_->subkeys_list_head_.load(std::memory_order_acquire),
          index_begin_, data_begin_};
}

void KeyView::Advance(Detail::VersionOffset version_offset) noexcept {
  assert(key_state_block_ != nullptr);
  AdvanceUntilSubkeysFound(
      key_state_block_->next_.load(std::memory_order_acquire), version_offset);
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
