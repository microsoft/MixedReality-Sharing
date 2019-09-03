// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <Microsoft/MixedReality/Sharing/VersionedStorage/AbstractKey.h>

#include <optional>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class Behavior;

// An implementation of AbstractKey that wraps an existing KeyHandle (and either
// owns it or not).
// If the handle is not owned, MakeHandle() operations will be duplicating the
// existing handle by calling Behavior::DuplicateHandle().
// Otherwise, the ownership will be transferred.
class AbstractKeyWithHandle : public AbstractKey {
 public:
  AbstractKeyWithHandle(Behavior& behavior,
                        KeyHandle key_handle,
                        bool has_handle_ownership) noexcept;

  AbstractKeyWithHandle(Behavior& behavior,
                        KeyHandle key_handle,
                        uint64_t key_hash,
                        bool has_handle_ownership) noexcept;

  ~AbstractKeyWithHandle() override;

  bool IsEqualTo(KeyHandle key) const noexcept override;
  bool IsLessThan(KeyHandle key) const noexcept override;
  bool IsGreaterThan(KeyHandle key) const noexcept override;
  KeyHandle MakeHandle() noexcept override;
  KeyHandle MakeHandle(KeyHandle existing_handle) noexcept override;

 protected:
  Behavior& behavior_;
  KeyHandle key_handle_;
  bool has_handle_ownership_;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
