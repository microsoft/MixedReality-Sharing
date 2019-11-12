// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/IteratorState.h>

#include <cassert>
#include <iterator>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Snapshot;
class KeyView;

class SubkeyIterator {
 public:
  using iterator_category = std::forward_iterator_tag;
  using value_type = SubkeyView;
  using difference_type = ptrdiff_t;
  using pointer = SubkeyView*;
  using reference = SubkeyView&;

  SubkeyIterator() noexcept = default;
  SubkeyIterator(const KeyView& key_view, const Snapshot& snapshot) noexcept;

  // Indicates the end of the range of subkeys.
  class End {};

  SubkeyIterator& operator++() noexcept {
    Advance();
    return *this;
  }

  SubkeyIterator operator++(int) noexcept {
    SubkeyIterator result = *this;
    Advance();
    return result;
  }

  bool operator==(const SubkeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(state_.version_ == other.state_.version_);
    return state_.current_state_block_ == other.state_.current_state_block_;
  }

  constexpr bool operator==(End) const noexcept {
    return state_.current_state_block_ == nullptr;
  }

  bool operator!=(const SubkeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(state_.version_ == other.state_.version_);
    return state_.current_state_block_ != other.state_.current_state_block_;
  }

  constexpr bool operator!=(End) const noexcept {
    return state_.current_state_block_ != nullptr;
  }

  SubkeyView operator*() const noexcept {
    assert(state_.current_state_block_ != nullptr);
    // We have to return a copy instead of a reference because advancing the
    // iterator will update the state of current_subkey_view_.
    return current_subkey_view_;
  }

  const SubkeyView* operator->() const noexcept {
    assert(state_.current_state_block_ != nullptr);
    return &current_subkey_view_;
  }

  constexpr bool is_end() const noexcept {
    return state_.current_state_block_ == nullptr;
  }

 private:
  void Advance() noexcept;

  Detail::SubkeyIteratorState state_;
  SubkeyView current_subkey_view_;
};

class SubkeyIteratorRange {
 public:
  constexpr SubkeyIteratorRange() noexcept = default;
  constexpr explicit SubkeyIteratorRange(SubkeyIterator begin) noexcept
      : begin_{begin} {}

  constexpr SubkeyIterator begin() const noexcept { return begin_; }
  constexpr SubkeyIterator::End end() const noexcept { return {}; }

 private:
  SubkeyIterator begin_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
