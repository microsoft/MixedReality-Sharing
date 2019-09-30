// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/layout.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class KeyView {
 public:
  constexpr KeyView() noexcept = default;
  constexpr KeyView(size_t subkeys_count,
                    Detail::KeyHandleWrapper* key_handle_wrapper) noexcept
      : subkeys_count_{subkeys_count},
        key_handle_wrapper_{key_handle_wrapper} {}

  // Returns a non-owning view (valid for as long as the snapshot is alive).
  KeyHandle key_handle() const noexcept {
    assert(key_handle_wrapper_);
    return key_handle_wrapper_->key_;
  }

  // Returns the number of subkeys for this key (for the observed version).
  constexpr uint64_t subkeys_count() const noexcept { return subkeys_count_; }

 private:
  size_t subkeys_count_{0};

  // Actually a Detail::KeyStateBlock* that is not accessible through
  // the public interface.
  Detail::KeyHandleWrapper* key_handle_wrapper_{nullptr};

  friend class KeyIterator;
  friend class SubkeyIterator;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
