// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include "src/IndexBlock.h"
#include "src/SubkeyStateView.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

#include <cassert>
#include <iterator>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

struct IndexBlockSlot;
class IndexBlock;

template <IndexLevel kLevel>
class BlockIterator : public std::iterator<std::forward_iterator_tag,
                                           StateAndIndexView<kLevel>> {
 public:
  class End {};

  BlockIterator() noexcept = default;

  BlockIterator(IndexSlotLocation first_location,
                IndexBlock* index_begin,
                std::byte* data_begin) noexcept
      : index_begin_{index_begin}, data_begin_{data_begin} {
    AdvanceTo(first_location);
  }

  BlockIterator& operator++() noexcept {
    assert(state_view_);
    AdvanceTo(state_view_.state_block_->next_.load(std::memory_order_acquire));
    return *this;
  }

  BlockIterator operator++(int) noexcept {
    assert(state_view_);
    BlockIterator result = *this;
    AdvanceTo(state_view_.state_block_->next_.load(std::memory_order_acquire));
    return result;
  }

  [[nodiscard]] constexpr bool operator==(const BlockIterator& other) const
      noexcept {
    return state_view_.index_block_slot_ == other.state_view_.index_block_slot_;
  }

  [[nodiscard]] constexpr bool operator==(End) const noexcept {
    return !state_view_;
  }

  [[nodiscard]] constexpr bool operator!=(const BlockIterator& other) const
      noexcept {
    return state_view_.index_block_slot_ != other.state_view_.index_block_slot_;
  }

  [[nodiscard]] constexpr bool operator!=(End) const noexcept {
    return !!state_view_;
  }

  StateAndIndexView<kLevel> operator*() const noexcept {
    assert(state_view_);
    return state_view_;
  }

  StateAndIndexView<kLevel>* operator->() noexcept {
    assert(state_view_);
    return &state_view_;
  }

 private:
  MS_MR_SHARING_FORCEINLINE
  void AdvanceTo(IndexSlotLocation location) noexcept {
    if (location != IndexSlotLocation::kInvalid) {
      IndexBlockSlot& slot = IndexBlock::GetSlot(index_begin_, location);

      state_view_.index_block_slot_ = &slot;
      state_view_.state_block_ = &GetBlockAt<StateBlock<kLevel>>(
          data_begin_, slot.state_block_location_);
      Platform::Prefetch(state_view_.state_block_);
      auto location =
          slot.version_block_location_.load(std::memory_order_acquire);
      if (location != DataBlockLocation::kInvalid) {
        // Almost all possible use cases will need to access the version block
        // soon after the state block, so it makes sense to start prefetching it
        // here unconditionally. Normal iterators (over snapshots etc.) will
        // care about it because they have to check if the key/subkey is even
        // present in the version. Special internal cases, such as the code that
        // reallocates the blob, will almost always want to know the most recent
        // version (and for that they will have to read the version block).
        state_view_.version_block_ =
            &GetBlockAt<VersionBlock<kLevel>>(data_begin_, location);
        Platform::Prefetch(state_view_.version_block_);
      } else {
        state_view_.version_block_ = nullptr;
      }
    } else {
      state_view_.index_block_slot_ = nullptr;
      state_view_.state_block_ = nullptr;
      state_view_.version_block_ = nullptr;
    }
  }

  StateAndIndexView<kLevel> state_view_;
  IndexBlock* index_begin_{nullptr};
  std::byte* data_begin_{nullptr};
};

using KeyBlockIterator = BlockIterator<IndexLevel::Key>;
using SubkeyBlockIterator = BlockIterator<IndexLevel::Subkey>;

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
