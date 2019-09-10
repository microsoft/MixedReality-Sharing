// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>

#include "src/HeaderBlock.h"
#include "src/StateBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

Snapshot::Snapshot(uint64_t version,
                   Detail::HeaderBlock& header_block,
                   size_t keys_count,
                   size_t subkeys_count,
                   std::shared_ptr<Behavior> behavior) noexcept
    : header_block_{header_block},
      behavior_{std::move(behavior)},
      version_{version},
      keys_count_{keys_count},
      subkeys_count_{subkeys_count} {}

Snapshot::~Snapshot() noexcept {
  header_block_.RemoveSnapshotReference(version_, *behavior_);
}

size_t Snapshot::GetSubkeysCount(const KeyDescriptor& key) const noexcept {
  return Detail::BlobAccessor{const_cast<Detail::HeaderBlock&>(header_block_)}
      .FindKey(version_, key);
}

std::optional<PayloadHandle> Snapshot::Get(const KeyDescriptor& key,
                                           uint64_t subkey) const noexcept {
  return Detail::BlobAccessor{const_cast<Detail::HeaderBlock&>(header_block_)}
      .FindSubkey(version_, key, subkey);
}

std::optional<KeyView> Snapshot::Get(const KeyDescriptor& key) const noexcept {
  Detail::BlobAccessor accessor{
      const_cast<Detail::HeaderBlock&>(header_block_)};

  Detail::KeyStateView key_state_view =
      Detail::BlobAccessor{const_cast<Detail::HeaderBlock&>(header_block_)}
          .FindKey(key);

  if (key_state_view) {
    if (auto subkeys_count =
            key_state_view.GetSubkeysCount(Detail::MakeVersionOffset(
                version_, header_block_.base_version()))) {
      return KeyView{version_, subkeys_count, key_state_view.state_block_,
                     accessor.index_begin_, accessor.data_begin_};
    }
  }
  return {};
}

KeyIterator Snapshot::begin() const noexcept {
  return {version_,
          Detail::MakeVersionOffset(version_, header_block_.base_version()),
          header_block_};
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
