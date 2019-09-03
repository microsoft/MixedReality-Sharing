// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <cassert>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// Vaguely mimics the C# IEnumerator interface for iterating over subkeys.
// All methods that return SubkeyEnumerator return it in a state where it points
// to the element before the first one (and therefore the behavior of Current()
// is undefined unless MoveNext() was called first and it returned true).
class SubkeyEnumerator {
 public:
  virtual ~SubkeyEnumerator() noexcept = default;

  uint64_t current_subkey() const noexcept {
    assert(has_current_);
    return current_subkey_;
  }

  PayloadHandle current_payload_handle() const noexcept {
    assert(has_current_);
    return current_payload_handle_;
  }

  virtual bool MoveNext() noexcept = 0;
  virtual void Reset() noexcept = 0;

 protected:
  SubkeyEnumerator() = default;
  SubkeyEnumerator(const SubkeyEnumerator&) = delete;
  SubkeyEnumerator& operator=(const SubkeyEnumerator&) = delete;

  bool has_current_ = false;
  uint64_t current_subkey_{0};
  PayloadHandle current_payload_handle_{0};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
