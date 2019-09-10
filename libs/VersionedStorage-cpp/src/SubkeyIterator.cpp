// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyIterator.h>

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

      Detail::VersionedPayloadHandle handle =
          version_block.GetVersionedPayload(version_);

      if (handle.has_payload()) {
        current_subkey_view_ = {current_state_block_->subkey_, handle.version(),
                                handle.payload()};
        return;
      }
    } else {
      Detail::VersionedPayloadHandle handle =
          current_state_block_->GetVersionedPayload(version_);
      if (handle.has_payload()) {
        current_subkey_view_ = {current_state_block_->subkey_, handle.version(),
                                handle.payload()};
        return;
      }
    }
    location = current_state_block_->next_.load(std::memory_order_acquire);
  }
  current_state_block_ = nullptr;
}

SubkeyIterator::SubkeyIterator(uint64_t version,
                               Detail::IndexSlotLocation index_slot_location,
                               Detail::IndexBlock* index_begin,
                               std::byte* data_begin) noexcept
    : version_{version}, index_begin_{index_begin}, data_begin_{data_begin} {
  AdvanceUntilPayloadFound(index_slot_location);
}

void SubkeyIterator::Advance() noexcept {
  assert(current_state_block_);
  AdvanceUntilPayloadFound(
      current_state_block_->next_.load(std::memory_order_acquire));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
