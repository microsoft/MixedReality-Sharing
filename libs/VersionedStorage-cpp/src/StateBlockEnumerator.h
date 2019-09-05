// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include "src/layout.h"

#include <cstddef>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class IndexBlock;
class KeyStateBlock;
class SubkeyStateBlock;
class KeyVersionBlock;
class SubkeyVersionBlock;

template <IndexLevel kLevel>
class StateBlockEnumeratorBase {
 public:
  StateBlock<kLevel>& CurrentStateBlock() noexcept {
    assert(current_state_block_);
    return *current_state_block_;
  }

  VersionBlock<kLevel>* CurrentVersionBlock() noexcept {
    assert(current_state_block_);
    return current_version_block_;
  }

  BlockStateSearchResult<kLevel> Current() noexcept {
    assert(current_state_block_);
    return {current_index_, current_state_block_, current_version_block_};
  }

  bool MoveNext() noexcept;

  void Reset() noexcept {
    next_ = begin_;
    current_state_block_ = nullptr;
    current_version_block_ = nullptr;
    current_index_ = IndexSlotLocation::kInvalid;
  }

 protected:
  StateBlockEnumeratorBase(IndexSlotLocation begin,
                           IndexBlock* index_begin,
                           std::byte* data_begin) noexcept
      : begin_{begin},
        next_{begin},
        index_begin_{index_begin},
        data_begin_{data_begin} {}

  IndexSlotLocation begin_;
  IndexSlotLocation next_;
  IndexBlock* const index_begin_;
  std::byte* const data_begin_;
  StateBlock<kLevel>* current_state_block_ = nullptr;
  VersionBlock<kLevel>* current_version_block_ = nullptr;
  IndexSlotLocation current_index_ = IndexSlotLocation::kInvalid;
};

class SubkeyStateBlockEnumerator
    : public StateBlockEnumeratorBase<IndexLevel::Subkey> {
 public:
  SubkeyStateBlockEnumerator(IndexSlotLocation begin,
                             IndexBlock* index_begin,
                             std::byte* data_begin)
      : StateBlockEnumeratorBase{begin, index_begin, data_begin} {}

  VersionedPayloadHandle GetPayload(uint64_t version) const noexcept;

  VersionedPayloadHandle latest_versioned_payload_thread_unsafe() const
      noexcept;
};

class KeyStateBlockEnumerator
    : public StateBlockEnumeratorBase<IndexLevel::Key> {
 public:
  KeyStateBlockEnumerator(IndexSlotLocation begin,
                          IndexBlock* index_begin,
                          std::byte* data_begin,
                          uint64_t versions_begin)
      : StateBlockEnumeratorBase{begin, index_begin, data_begin},
        versions_begin_{versions_begin} {}

  SubkeyStateBlockEnumerator CreateSubkeyStateBlockEnumerator() const noexcept;

  uint32_t GetSubkeysCount(uint64_t version) const noexcept;

  uint32_t latest_subkeys_count_thread_unsafe() const noexcept;

 private:
  uint64_t versions_begin_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
