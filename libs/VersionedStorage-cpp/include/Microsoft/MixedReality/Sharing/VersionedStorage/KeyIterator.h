// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyView.h>

#include <cassert>
#include <cstddef>
#include <iterator>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Snapshot;

class KeyIterator : public std::iterator<std::forward_iterator_tag, KeyView> {
 public:
  KeyIterator() noexcept = default;
  KeyIterator(const Snapshot& snapshot) noexcept;

  // Indicates the end of the range of keys.
  class End {};

  KeyIterator& operator++() noexcept {
    Advance();
    return *this;
  }

  KeyIterator operator++(int) noexcept {
    KeyIterator result = *this;
    Advance();
    return result;
  }

  bool operator==(const KeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(version_offset_ == other.version_offset_);
    return current_key_view_.key_handle_wrapper_ ==
           other.current_key_view_.key_handle_wrapper_;
  }

  constexpr bool operator==(End) const noexcept {
    return current_key_view_.key_handle_wrapper_ == nullptr;
  }

  bool operator!=(const KeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(version_offset_ == other.version_offset_);
    return current_key_view_.key_handle_wrapper_ !=
           other.current_key_view_.key_handle_wrapper_;
  }

  constexpr bool operator!=(End) const noexcept {
    return current_key_view_.key_handle_wrapper_ != nullptr;
  }

  KeyView operator*() const noexcept {
    assert(current_key_view_.key_handle_wrapper_ != nullptr);
    // We have to return a copy instead of a reference because advancing the
    // iterator will update the state of current_key_view_.
    return current_key_view_;
  }

  const KeyView* operator->() const noexcept {
    assert(current_key_view_.key_handle_wrapper_ != nullptr);
    return &current_key_view_;
  }

  constexpr bool is_end() const noexcept {
    return current_key_view_.key_handle_wrapper_ == nullptr;
  }

 private:
  void Advance() noexcept;
  void AdvanceUntilSubkeysFound(Detail::IndexSlotLocation location) noexcept;

  KeyView current_key_view_;
  Detail::VersionOffset version_offset_{0};
  Detail::IndexBlock* index_begin_{nullptr};
  std::byte* data_begin_{nullptr};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
