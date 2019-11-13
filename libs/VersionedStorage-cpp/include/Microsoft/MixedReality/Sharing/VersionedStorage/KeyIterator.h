// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyView.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/BlobLayout.h>

#include <cassert>
#include <cstddef>
#include <iterator>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Snapshot;

class KeyIterator {
 public:
  using iterator_category = std::forward_iterator_tag;
  using value_type = KeyView;
  using difference_type = ptrdiff_t;
  using pointer = KeyView*;
  using reference = KeyView&;

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

  [[nodiscard]] bool operator==(const KeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(version_offset_ == other.version_offset_);
    return current_key_view_.key_handle_wrapper_ ==
           other.current_key_view_.key_handle_wrapper_;
  }

  [[nodiscard]] constexpr bool operator==(End) const noexcept {
    return current_key_view_.key_handle_wrapper_ == nullptr;
  }

  [[nodiscard]] bool operator!=(const KeyIterator& other) const noexcept {
    // Should only be called for iterators related to the same version.
    assert(version_offset_ == other.version_offset_);
    return current_key_view_.key_handle_wrapper_ !=
           other.current_key_view_.key_handle_wrapper_;
  }

  [[nodiscard]] constexpr bool operator!=(End) const noexcept {
    return current_key_view_.key_handle_wrapper_ != nullptr;
  }

  [[nodiscard]] KeyView operator*() const noexcept {
    assert(current_key_view_.key_handle_wrapper_ != nullptr);
    // We have to return a copy instead of a reference because advancing the
    // iterator will update the state of current_key_view_.
    return current_key_view_;
  }

  const KeyView* operator->() const noexcept {
    assert(current_key_view_.key_handle_wrapper_ != nullptr);
    return &current_key_view_;
  }

  [[nodiscard]] constexpr bool is_end() const noexcept {
    return current_key_view_.key_handle_wrapper_ == nullptr;
  }

 private:
  void Advance() noexcept;
  void AdvanceUntilSubkeysFound(Detail::IndexSlotLocation location) noexcept;

  KeyView current_key_view_;
  Detail::VersionOffset version_offset_{0};
  Detail::BlobLayout blob_layout_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
