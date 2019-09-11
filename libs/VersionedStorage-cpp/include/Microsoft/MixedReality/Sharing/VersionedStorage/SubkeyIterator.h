// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/layout.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyView.h>

#include <cassert>
#include <cstddef>
#include <iterator>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Snapshot;
class KeyView;

class SubkeyIterator
    : public std::iterator<std::forward_iterator_tag, SubkeyView> {
 public:
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
    assert(version_ == other.version_);
    return current_state_block_ == other.current_state_block_;
  }

  constexpr bool operator==(End) const noexcept {
    return current_state_block_ == nullptr;
  }

  bool operator!=(const SubkeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(version_ == other.version_);
    return current_state_block_ != other.current_state_block_;
  }

  constexpr bool operator!=(End) const noexcept {
    return current_state_block_ != nullptr;
  }

  SubkeyView operator*() const noexcept {
    assert(current_state_block_ != nullptr);
    // We have to return a copy instead of a reference because advancing the
    // iterator will update the state of current_subkey_view_.
    return current_subkey_view_;
  }

  const SubkeyView* operator->() const noexcept {
    assert(current_state_block_ != nullptr);
    return &current_subkey_view_;
  }

  constexpr bool is_end() const noexcept {
    return current_state_block_ == nullptr;
  }

 private:
  void Advance() noexcept;
  void AdvanceUntilPayloadFound(Detail::IndexSlotLocation location) noexcept;

  uint64_t version_{0};
  SubkeyView current_subkey_view_;
  Detail::SubkeyStateBlock* current_state_block_ = nullptr;
  Detail::IndexBlock* index_begin_ = nullptr;
  std::byte* data_begin_ = nullptr;
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
