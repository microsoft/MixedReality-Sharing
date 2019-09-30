// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptorWithHandle.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Behavior.h>

#include <cassert>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

KeyDescriptorWithHandle::KeyDescriptorWithHandle(
    Behavior& behavior,
    KeyHandle key_handle,
    bool has_handle_ownership) noexcept
    : KeyDescriptor{behavior.GetHash(key_handle)},
      behavior_{behavior},
      key_handle_{key_handle},
      has_handle_ownership_{has_handle_ownership} {}

KeyDescriptorWithHandle::KeyDescriptorWithHandle(
    Behavior& behavior,
    KeyHandle key_handle,
    uint64_t key_hash,
    bool has_handle_ownership) noexcept
    : KeyDescriptor{key_hash},
      behavior_{behavior},
      key_handle_{key_handle},
      has_handle_ownership_{has_handle_ownership} {
  assert(behavior.GetHash(key_handle) == key_hash);
}

KeyDescriptorWithHandle::~KeyDescriptorWithHandle() {
  if (has_handle_ownership_) {
    behavior_.Release(key_handle_);
  }
}

bool KeyDescriptorWithHandle::IsEqualTo(KeyHandle key) const noexcept {
  return behavior_.Equal(key_handle_, key);
}

bool KeyDescriptorWithHandle::IsLessThan(KeyHandle key) const noexcept {
  return behavior_.Less(key_handle_, key);
}

bool KeyDescriptorWithHandle::IsGreaterThan(KeyHandle key) const noexcept {
  return behavior_.Less(key, key_handle_);
}

KeyHandle KeyDescriptorWithHandle::MakeHandle() noexcept {
  if (has_handle_ownership_) {
    has_handle_ownership_ = false;
    return key_handle_;
  }
  return behavior_.DuplicateHandle(key_handle_);
}

KeyHandle KeyDescriptorWithHandle::MakeHandle(KeyHandle) noexcept {
  // The provided handle is ignored since this implementation already has a
  // handle.
  return MakeHandle();
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
