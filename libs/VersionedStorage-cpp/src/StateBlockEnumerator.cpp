// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include "src/StateBlockEnumerator.h"

#include "src/IndexBlock.h"
#include "src/KeyVersionBlock.h"
#include "src/StateBlock.h"
#include "src/SubkeyVersionBlock.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

template <IndexLevel kLevel>
bool StateBlockEnumeratorBase<kLevel>::MoveNext() noexcept {
  if (next_ == IndexSlotLocation::kInvalid)
    return false;

  const IndexBlock::Slot& slot = IndexBlock::GetSlot(index_begin_, next_);
  current_state_block_ =
      &GetBlockAt<StateBlock<kLevel>>(data_begin_, slot.state_block_location_);
  Platform::Prefetch(current_state_block_);
  const DataBlockLocation version_block_location =
      slot.version_block_location_.load(std::memory_order_acquire);
  if (version_block_location != DataBlockLocation::kInvalid) {
    // This can be either SubkeyVersionBlock or KeyVersionBlock,
    // but we cast to void* anyway since there is currently no base class.
    current_version_block_ =
        &GetBlockAt<VersionBlock<kLevel>>(data_begin_, version_block_location);
    Platform::Prefetch(current_version_block_);
  } else {
    current_version_block_ = nullptr;
  }
  current_index_ = next_;
  next_ = current_state_block_->next_.load(std::memory_order_acquire);
  return true;
}

template bool StateBlockEnumeratorBase<IndexLevel::Key>::MoveNext() noexcept;

template bool StateBlockEnumeratorBase<IndexLevel::Subkey>::MoveNext() noexcept;

VersionedPayloadHandle SubkeyStateBlockEnumerator::GetPayload(
    uint64_t version) const noexcept {
  assert(current_state_block_);
  return current_version_block_
             ? current_version_block_->GetVersionedPayload(version)
             : current_state_block_->GetVersionedPayload(version);
}

VersionedPayloadHandle SubkeyStateBlockEnumerator::GetLatestPayload() const
    noexcept {
  assert(current_state_block_);
  return current_version_block_
             ? current_version_block_->GetLatestVersionedPayload()
             : current_state_block_->GetLatestVersionedPayload();
}

SubkeyStateBlockEnumerator
KeyStateBlockEnumerator::CreateSubkeyStateBlockEnumerator() const noexcept {
  assert(current_state_block_);
  return {
      current_state_block_->subkeys_list_head_.load(std::memory_order_acquire),
      index_begin_, data_begin_};
}

uint32_t KeyStateBlockEnumerator::GetSubkeysCount(uint64_t version) const
    noexcept {
  assert(current_state_block_);
  if (IsVersionConvertibleToOffset(version, versions_begin_)) {
    const VersionOffset offset = MakeVersionOffset(version, versions_begin_);
    return current_version_block_
               ? current_version_block_->GetSubkeysCount(offset)
               : current_state_block_->GetSubkeysCount(offset);
  }
  return 0;
}

uint32_t KeyStateBlockEnumerator::GetLatestSubkeysCount() const noexcept {
  return current_version_block_
             ? current_version_block_->GetLatestSubkeysCount()
             : current_state_block_->GetLatestSubkeysCount();
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
