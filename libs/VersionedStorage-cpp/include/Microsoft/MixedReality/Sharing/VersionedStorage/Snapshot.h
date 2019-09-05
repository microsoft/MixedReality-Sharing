// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <memory>
#include <optional>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

class KeyDescriptor;
class Behavior;
class HeaderBlock;
class KeyEnumerator;
class Storage;
class SubkeyEnumerator;

// An immutable object representing the state of the Storage at some specific
// version.
// A snapshot can be taken from the Storage at any time, and it is going to
// reference the same state for as long as it's alive.
// There is no specific limit on the number of alive snapshots, although having
// too many may result in the storage running out of memory.
class Snapshot : public std::enable_shared_from_this<Snapshot> {
 public:
  // Doesn't increment any reference counts (they should be pre-incremented).
  Snapshot(uint64_t version,
           HeaderBlock& header_block,
           std::shared_ptr<Behavior> behavior) noexcept;
  ~Snapshot() noexcept;

  constexpr uint64_t version() const noexcept { return version_; }
  constexpr size_t keys_count() const noexcept { return keys_count_; }
  constexpr size_t subkeys_count() const noexcept { return subkeys_count_; }
  size_t GetSubkeysCount(const KeyDescriptor& key) const noexcept;
  std::optional<PayloadHandle> Get(const KeyDescriptor& key,
                                   uint64_t subkey) const noexcept;

  std::unique_ptr<KeyEnumerator> CreateKeyEnumerator() const noexcept;
  std::unique_ptr<SubkeyEnumerator> CreateSubkeyEnumerator(
      const KeyDescriptor& key) const noexcept;

 private:
  Snapshot(const Snapshot&) = delete;
  Snapshot& operator=(const Snapshot&) = delete;

  HeaderBlock& header_block_;
  std::shared_ptr<Behavior> behavior_;
  const uint64_t version_;
  const size_t keys_count_{0};
  const size_t subkeys_count_{0};
  friend class Storage;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
