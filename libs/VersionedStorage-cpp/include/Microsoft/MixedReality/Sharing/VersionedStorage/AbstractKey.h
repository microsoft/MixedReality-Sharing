// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// Abstract key-like object that can be passed to the storage implementation to
// interact with other KeyHandle objects without actually creating a KeyHandle
// (when doing so would be expensive).
//
// For example, if the application uses interned strings as keys, and wants to
// look for a string_view, it can implement AbstractKey that will work with
// string_view directly, without creating an interned string first,
// assuming that all operations are consistent with the actual KeyHandle.
//
// Most AbstractKey objects should be created on the stack and discarded after
// use if used with mutating operations, such as inserting a new subkey, because
// these operations are allowed to call non-const methods, which are allowed to
// mutate AbstractKey, rendering it unusable for any further calls.
class AbstractKey {
 public:
  [[nodiscard]] constexpr uint64_t hash() const noexcept { return key_hash_; }

  [[nodiscard]] virtual bool IsEqualTo(KeyHandle key) const noexcept = 0;

  // Must be consistent with Behavior::Less().
  [[nodiscard]] virtual bool IsLessThan(KeyHandle key) const noexcept = 0;

  // Must be consistent with Behavior::Less().
  [[nodiscard]] virtual bool IsGreaterThan(KeyHandle key) const noexcept = 0;

  // Returns a handle to the key that behaves the same way as this AbstractKey
  // object.
  // No other methods will be called after this one, so if this
  // AbstractKey owns the handle already, it can just transfer the
  // ownership (possibly making this object unusable).
  [[nodiscard]] virtual KeyHandle MakeHandle() noexcept = 0;

  // Returns a handle to the key that behaves the same way as this AbstractKey
  // object.
  // Receives a handle to effectively the same key as an argument (for which
  // Equals() returned true). The ownership to existing_handle is not
  // transferred, it is still owned by the caller.
  // The implementation is allowed to duplicate existing_handle if it's
  // cheaper (for example, if keys are interned reference counted objects,
  // and this AbstractKey doesn't own a KeyHandle already, it may be cheaper
  // to add a reference to existing_handle).
  // It can also ignore the hint and behave the same way as another MakeHandle()
  [[nodiscard]] virtual KeyHandle MakeHandle(
      KeyHandle existing_handle) noexcept = 0;

 protected:
  AbstractKey(uint64_t key_hash) : key_hash_(key_hash) {}
  virtual ~AbstractKey() = default;
  AbstractKey(const AbstractKey&) = delete;
  AbstractKey& operator=(const AbstractKey&) = delete;
  const uint64_t key_hash_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
