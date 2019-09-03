// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// A class that customizes the semantic of the storage, and hides all details
// about the nature of keys and payloads.
// For example, if keys and values are reference counted objects, Behavior
// object should implement the conversion between handles and pointers where
// necessary, and add/remove references in DuplicateHandle/Release calls.
class Behavior {
 public:
  // Returns the hash of the key associated with the handle.
  // If the behavior is used with a replicated storage, the hash must never
  // depend on non-deterministic conditions, such as addresses of allocated
  // keys.
  virtual uint64_t GetHash(KeyHandle handle) const noexcept = 0;

  virtual bool Equal(KeyHandle a, KeyHandle b) const noexcept = 0;
  virtual bool Less(KeyHandle a, KeyHandle b) const noexcept = 0;

  // Returns true if payloads are identical.
  // The implementation is allowed to just compare the handles if comparing the
  // payloads is impractical. Doing so will have the following effect:
  // * Transactions are not allowed to use payloads as prerequisites
  //   (because they are always checked with this method, and therefore the
  //   check will fail).
  // * Transactions may change the subkey to the same value,
  //   and then trigger subscription callbacks with identical
  //   "before" and "after" values.
  virtual bool Equal(PayloadHandle a, PayloadHandle b) const noexcept = 0;

  virtual void Release(KeyHandle handle) noexcept = 0;
  virtual void Release(PayloadHandle handle) noexcept = 0;
  virtual void Release(KeySubscriptionHandle handle) noexcept = 0;
  virtual void Release(SubkeySubscriptionHandle handle) noexcept = 0;

  // The implementation is allowed to return the same handle if it can just
  // increment its reference count, or if references are irrelevant
  // (for example, if the handle represents the integer key).
  // Duplicating a handle must always result in a handle for which GetHash(),
  // Equal() and Less() will behave the same way as for the original handle.
  virtual KeyHandle DuplicateHandle(KeyHandle handle) noexcept = 0;

  // The implementation is allowed to return the same handle if it can just
  // increment its reference count, or if references are irrelevant
  // (for example, if the handle represents the integer payload).
  // Duplicating a handle must always result in a handle for which Equal() will
  // behave the same way as for the original handle.
  virtual PayloadHandle DuplicateHandle(PayloadHandle handle) noexcept = 0;

  // Allocates pages_count pages, each is 4096 bytes large.
  // The returned address should be page-aligned.
  // Returns nullptr if the allocation is not possible.
  virtual void* AllocateZeroedPages(size_t pages_count) noexcept = 0;

  // Frees pages previously allocated with AllocateZeroedPages.
  virtual void FreePages(void* address) noexcept = 0;

  // Locks the mutex that restricts all modifications of the storage.
  // This is customizable for the cases where the storage blobs are located in
  // shared memory and modified by multiple processes (in which case the
  // implementation can use a cross-process OS mutex).
  virtual void LockWriterMutex() noexcept = 0;

  // Unlocks the mutex that restricts all modifications of the storage.
  virtual void UnlockWriterMutex() noexcept = 0;

 protected:
  Behavior() = default;
  virtual ~Behavior() = default;
  Behavior(const Behavior&) = delete;
  Behavior& operator=(const Behavior&) = delete;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
