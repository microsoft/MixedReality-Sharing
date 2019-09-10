// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyIterator.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <optional>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace Detail {
class HeaderBlock;
}

class KeyDescriptor;
class Behavior;
class Storage;

// References an immutable state of the storage at some specific version.
// A snapshot can be taken from the Storage at any time, and it is going to
// reference the same state for as long as it's alive.
// There is no specific limit on the number of alive snapshots, although having
// too many may result in the storage running out of memory.
class Snapshot {
 public:
  ~Snapshot() noexcept;

  Snapshot() noexcept;

  // Doesn't increment any reference counts (they should be pre-incremented).
  Snapshot(uint64_t version,
           Detail::HeaderBlock& header_block,
           size_t keys_count,
           size_t subkeys_count,
           std::shared_ptr<Behavior> behavior) noexcept;

  Snapshot(Snapshot&&) noexcept;
  Snapshot(const Snapshot&) noexcept;
  Snapshot& operator=(Snapshot&&) noexcept;
  Snapshot& operator=(const Snapshot&) noexcept;

  constexpr uint64_t version() const noexcept { return version_; }
  constexpr size_t keys_count() const noexcept { return keys_count_; }
  constexpr size_t subkeys_count() const noexcept { return subkeys_count_; }
  size_t GetSubkeysCount(const KeyDescriptor& key) const noexcept;

  // FIXME: change the interface
  std::optional<PayloadHandle> Get(const KeyDescriptor& key,
                                   uint64_t subkey) const noexcept;

  std::optional<KeyView> Get(const KeyDescriptor& key) const noexcept;

  KeyIterator begin() const noexcept;
  KeyIteratorEnd end() const noexcept { return {}; }

 private:
  Detail::HeaderBlock* header_block_{nullptr};
  std::shared_ptr<Behavior> behavior_;
  uint64_t version_{0};
  size_t keys_count_{0};
  size_t subkeys_count_{0};
  friend class Storage;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
