// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyDescriptor.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Behavior;

// An implementation of KeyDescriptor that wraps an existing KeyHandle (and
// either owns it or not).
// If the handle is not owned, MakeHandle() operations will be duplicating
// the existing handle by calling Behavior::DuplicateHandle(). Otherwise,
// the ownership will be transferred.
class KeyDescriptorWithHandle : public KeyDescriptor {
 public:
  KeyDescriptorWithHandle(Behavior& behavior,
                          KeyHandle key_handle,
                          bool has_handle_ownership) noexcept;

  KeyDescriptorWithHandle(Behavior& behavior,
                          KeyHandle key_handle,
                          uint64_t key_hash,
                          bool has_handle_ownership) noexcept;

  ~KeyDescriptorWithHandle() override;

  bool IsEqualTo(KeyHandle key) const noexcept override;
  bool IsLessThan(KeyHandle key) const noexcept override;
  bool IsGreaterThan(KeyHandle key) const noexcept override;
  KeyHandle MakeHandle() noexcept override;
  KeyHandle MakeHandle(KeyHandle existing_handle) noexcept override;

  void ReplaceHandle(KeyHandle key_handle, bool has_handle_ownership) noexcept;

 protected:
  Behavior& behavior_;
  KeyHandle key_handle_;
  bool has_handle_ownership_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
