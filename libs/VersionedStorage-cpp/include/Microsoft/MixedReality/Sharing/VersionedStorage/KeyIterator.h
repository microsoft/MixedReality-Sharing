// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyView.h>

#include <cassert>
#include <cstddef>
#include <iterator>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// Indicates the end of the range of keys.
class KeyIteratorEnd {};

class KeyIterator : public std::iterator<std::forward_iterator_tag, KeyView> {
 public:
  KeyIterator(uint64_t observed_version,
              Detail::VersionOffset version_offset,
              Detail::HeaderBlock& header_block) noexcept
      : current_key_view_{observed_version, version_offset, header_block},
        version_offset_{version_offset} {}

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
    assert(current_key_view_.observed_version_ ==
           other.current_key_view_.observed_version_);
    return current_key_view_.key_state_block_ ==
           other.current_key_view_.key_state_block_;
  }

  constexpr bool operator==(KeyIteratorEnd) const noexcept {
    return current_key_view_.key_state_block_ == nullptr;
  }

  bool operator!=(const KeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(current_key_view_.observed_version_ ==
           other.current_key_view_.observed_version_);
    return current_key_view_.key_state_block_ !=
           other.current_key_view_.key_state_block_;
  }

  constexpr bool operator!=(KeyIteratorEnd) const noexcept {
    return current_key_view_.key_state_block_ != nullptr;
  }

  KeyView operator*() const noexcept {
    assert(current_key_view_.key_state_block_ != nullptr);
    // We have to return a copy instead of a reference because advancing the
    // iterator will update the state of current_key_view_.
    return current_key_view_;
  }

  const KeyView* operator->() const noexcept {
    assert(current_key_view_.key_state_block_ != nullptr);
    return &current_key_view_;
  }

  constexpr bool is_end() const noexcept {
    return current_key_view_.key_state_block_ == nullptr;
  }

 private:
  void Advance() noexcept { current_key_view_.Advance(version_offset_); }

  KeyView current_key_view_;
  Detail::VersionOffset version_offset_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
