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
    // Almost all possible use cases will need to access the version block soon
    // after the state block, so it makes sense to start prefetching it here
    // unconditionally. Normal iterators (over snapshots etc.) will care about
    // it because they have to check if the key/subkey is even present in the
    // version. Special internal cases, such as the code that reallocates the
    // blob, will almost always want to know the most recent version (and for
    // that they will have to read the version block).
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

VersionedPayloadHandle
SubkeyStateBlockEnumerator::latest_versioned_payload_thread_unsafe() const
    noexcept {
  assert(current_state_block_);
  return current_version_block_
             ? current_version_block_->latest_versioned_payload_thread_unsafe()
             : current_state_block_->latest_versioned_payload_thread_unsafe();
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

uint32_t KeyStateBlockEnumerator::latest_subkeys_count_thread_unsafe() const
    noexcept {
  return current_version_block_
             ? current_version_block_->latest_subkeys_count_thread_unsafe()
             : current_state_block_->latest_subkeys_count_thread_unsafe();
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
