// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <string_view>
#include <vector>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// A class that customizes the semantic of the storage, and hides all details
// about the nature of keys and payloads.
// For example, if keys and values are reference counted objects, the Behavior
// object should implement the conversion between handles and pointers where
// necessary, and add/remove references in DuplicateHandle/Release calls.
class Behavior {
 public:
  // Returns the hash of the key associated with the handle.
  // If the behavior is used with a replicated storage, the hash must never
  // depend on non-deterministic conditions, such as addresses of allocated
  // keys.
  [[nodiscard]] virtual uint64_t GetKeyHash(KeyHandle handle) const
      noexcept = 0;
  [[nodiscard]] virtual uint64_t GetKeyHash(
      std::string_view serialized_key_handle) const noexcept = 0;

  [[nodiscard]] virtual bool Equal(KeyHandle a, KeyHandle b) const noexcept = 0;
  [[nodiscard]] virtual bool Equal(KeyHandle key_handle,
                                   std::string_view serialized_payload) const
      noexcept = 0;

  [[nodiscard]] virtual bool Less(KeyHandle a, KeyHandle b) const noexcept = 0;
  [[nodiscard]] virtual bool Less(std::string_view a, KeyHandle b) const
      noexcept = 0;
  [[nodiscard]] virtual bool Less(KeyHandle a, std::string_view b) const
      noexcept = 0;

  // Returns true if payloads are identical.
  // The implementation is allowed to just compare the handles if comparing the
  // payloads is impractical. Doing so will have the following effects:
  // * Transactions are not allowed to use payloads as prerequisites
  //   (because they are always checked with this method, and therefore the
  //   check will fail).
  // * Transactions that change the subkey to the same value it already has will
  //   do it explicitly, triggering subscription callbacks with identical
  //   "before" and "after" values (normally Equal() is called to filter-out
  //   unnecessary changes).
  [[nodiscard]] virtual bool Equal(PayloadHandle a, PayloadHandle b) const
      noexcept = 0;

  [[nodiscard]] virtual bool Equal(PayloadHandle payload_handle,
                                   std::string_view serialized_payload) const
      noexcept = 0;

  virtual void Release(KeyHandle handle) noexcept = 0;
  virtual void Release(PayloadHandle handle) noexcept = 0;
  virtual void Release(KeySubscriptionHandle handle) noexcept = 0;
  virtual void Release(SubkeySubscriptionHandle handle) noexcept = 0;

  // The implementation is allowed to return the same handle if it can just
  // increment its reference count, or if references are irrelevant
  // (for example, if the handle represents the integer key).
  // Duplicating a handle must always result in a handle for which GetHash(),
  // Equal() and Less() will behave the same way as for the original handle.
  [[nodiscard]] virtual KeyHandle DuplicateHandle(
      KeyHandle handle) noexcept = 0;

  // The implementation is allowed to return the same handle if it can just
  // increment its reference count, or if references are irrelevant
  // (for example, if the handle represents the integer payload).
  // Duplicating a handle must always result in a handle for which Equal() will
  // behave the same way as for the original handle.
  [[nodiscard]] virtual PayloadHandle DuplicateHandle(
      PayloadHandle handle) noexcept = 0;

  // Allocates pages_count pages, each is 4096 bytes large.
  // The returned address should be page-aligned.
  // Returns nullptr if the allocation is not possible.
  [[nodiscard]] virtual void* AllocateZeroedPages(
      size_t pages_count) noexcept = 0;

  // Frees pages previously allocated with AllocateZeroedPages.
  virtual void FreePages(void* address) noexcept = 0;

  // Serializes the key associated with the provided handle and returns the
  // number of written bytes.
  // The serialized data doesn't have to contain this size, as it will be saved
  // separately and provided to the deserialization code (so, if the key is a
  // string, the implementation can just append the string's data as is, without
  // any size prefix or terminator).
  // FIXME: make all Serialize methods noexcept
  virtual size_t Serialize(KeyHandle handle,
                           std::vector<std::byte>& byte_stream) = 0;

  virtual size_t Serialize(PayloadHandle handle,
                           std::vector<std::byte>& byte_stream) = 0;

  virtual KeyHandle DeserializeKey(std::string_view serialized_payload) = 0;

  virtual PayloadHandle DeserializePayload(
      std::string_view serialized_payload) = 0;

 protected:
  Behavior() = default;
  virtual ~Behavior() = default;
  Behavior(const Behavior&) = delete;
  Behavior& operator=(const Behavior&) = delete;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
