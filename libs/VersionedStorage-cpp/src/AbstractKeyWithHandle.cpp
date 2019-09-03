// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/AbstractKeyWithHandle.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Behavior.h>

#include <cassert>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

AbstractKeyWithHandle::AbstractKeyWithHandle(Behavior& behavior,
                                             KeyHandle key_handle,
                                             bool has_handle_ownership) noexcept
    : AbstractKey{behavior.GetHash(key_handle)},
      behavior_{behavior},
      key_handle_{key_handle},
      has_handle_ownership_{has_handle_ownership} {}

AbstractKeyWithHandle::AbstractKeyWithHandle(Behavior& behavior,
                                             KeyHandle key_handle,
                                             uint64_t key_hash,
                                             bool has_handle_ownership) noexcept
    : AbstractKey{key_hash},
      behavior_{behavior},
      key_handle_{key_handle},
      has_handle_ownership_{has_handle_ownership} {
  assert(behavior.GetHash(key_handle) == key_hash);
}

AbstractKeyWithHandle::~AbstractKeyWithHandle() {
  if (has_handle_ownership_) {
    behavior_.Release(key_handle_);
  }
}

bool AbstractKeyWithHandle::IsEqualTo(KeyHandle key) const noexcept {
  return behavior_.Equal(key_handle_, key);
}

bool AbstractKeyWithHandle::IsLessThan(KeyHandle key) const noexcept {
  return behavior_.Less(key_handle_, key);
}

bool AbstractKeyWithHandle::IsGreaterThan(KeyHandle key) const noexcept {
  return behavior_.Less(key, key_handle_);
}

KeyHandle AbstractKeyWithHandle::MakeHandle() noexcept {
  if (has_handle_ownership_) {
    has_handle_ownership_ = false;
    return key_handle_;
  }
  return behavior_.DuplicateHandle(key_handle_);
}

KeyHandle AbstractKeyWithHandle::MakeHandle(KeyHandle) noexcept {
  // The provided handle is ignored since this implementation already has a
  // handle.
  return MakeHandle();
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
