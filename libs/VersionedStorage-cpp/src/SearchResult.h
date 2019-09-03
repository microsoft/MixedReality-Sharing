// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include "src/KeyVersionBlock.h"
#include "src/StateBlock.h"
#include "src/SubkeyVersionBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

template <IndexLevel kLevel>
class SearchResult {
 public:
  SearchResult() noexcept = default;
  SearchResult(IndexSlotLocation index_slot_location,
               const StateBlock<kLevel>* state_block,
               ValueType<kLevel> value)
      : index_slot_location_{index_slot_location},
        state_block_{state_block},
        value_{value} {}

  auto index_slot_location() const noexcept { return index_slot_location_; }
  auto state_block() const noexcept { return state_block_; }
  auto value() const noexcept { return value_; }

 protected:
  IndexSlotLocation index_slot_location_{IndexSlotLocation::kInvalid};
  const StateBlock<kLevel>* state_block_{nullptr};
  ValueType<kLevel> value_{};
};

template <IndexLevel kLevel>
class BlockStateSearchResultBase {
 public:
  BlockStateSearchResultBase() noexcept = default;
  BlockStateSearchResultBase(IndexSlotLocation index_slot_location,
                             StateBlock<kLevel>* state_block,
                             VersionBlock<kLevel>* version_block) noexcept
      : index_slot_location_{index_slot_location},
        state_block_{state_block},
        version_block_{version_block} {}

  constexpr bool is_state_block_found() const noexcept {
    return state_block_ != nullptr;
  }

  IndexSlotLocation index_slot_location_{IndexSlotLocation::kInvalid};
  StateBlock<kLevel>* state_block_{nullptr};
  VersionBlock<kLevel>* version_block_{nullptr};
};

using KeySearchResult = SearchResult<IndexLevel::Key>;
using SubkeySearchResult = SearchResult<IndexLevel::Subkey>;

class KeyBlockStateSearchResult
    : public BlockStateSearchResultBase<IndexLevel::Key> {
 public:
  using BlockStateSearchResultBase::BlockStateSearchResultBase;

  uint32_t GetSubkeysCount(VersionOffset version_offset) const noexcept {
    if (version_block_)
      return version_block_->GetSubkeysCount(version_offset);
    if (state_block_)
      return state_block_->GetSubkeysCount(version_offset);
    return 0;
  }

  // Should only be called by the writer thread
  uint32_t GetLatestSubkeysCount() const noexcept {
    if (version_block_)
      return version_block_->GetLatestSubkeysCount();
    if (state_block_)
      return state_block_->GetLatestSubkeysCount();
    return 0;
  }
};

class SubkeyBlockStateSearchResult
    : public BlockStateSearchResultBase<IndexLevel::Subkey> {
 public:
  using BlockStateSearchResultBase::BlockStateSearchResultBase;

  VersionedPayloadHandle GetVersionedPayload(uint64_t version) const noexcept {
    if (version_block_)
      return version_block_->GetVersionedPayload(version);
    if (state_block_)
      return state_block_->GetVersionedPayload(version);
    return {};
  }

  // Should only be called by the writer thread
  VersionedPayloadHandle GetLatestVersionedPayload() const noexcept {
    if (version_block_)
      return version_block_->GetLatestVersionedPayload();
    if (state_block_)
      return state_block_->GetLatestVersionedPayload();
    return {};
  }
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
