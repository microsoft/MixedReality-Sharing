// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <cassert>
#include <memory>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class SubkeyEnumerator;

// Vaguely mimics the C# IEnumerator interface for iterating over keys.
// All methods that return KeyEnumerator return it in a state where it points to
// the element before the first one (and therefore the behavior of Current() is
// undefined unless MoveNext() was called first and it returned true).
class KeyEnumerator {
 public:
  virtual ~KeyEnumerator() noexcept = default;

  KeyHandle current_key() const noexcept {
    assert(has_current_);
    return current_key_;
  }

  size_t current_subkeys_count() const noexcept {
    assert(has_current_);
    return current_subkeys_count_;
  }

  virtual std::unique_ptr<SubkeyEnumerator> CreateSubkeyEnumerator() const
      noexcept = 0;

  [[nodiscard]] virtual bool MoveNext() noexcept = 0;
  virtual void Reset() noexcept = 0;

 protected:
  KeyEnumerator() = default;
  KeyEnumerator(const KeyEnumerator&) = delete;
  KeyEnumerator& operator=(const KeyEnumerator&) = delete;

  bool has_current_{false};
  KeyHandle current_key_{0};
  size_t current_subkeys_count_{0};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
