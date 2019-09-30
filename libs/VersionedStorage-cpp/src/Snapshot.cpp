// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>

#include "src/HeaderBlock.h"
#include "src/StateBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

Snapshot::~Snapshot() noexcept {
  if (header_block_)
    header_block_->RemoveSnapshotReference(version_, *behavior_);
}

Snapshot::Snapshot(uint64_t version,
                   Detail::HeaderBlock& header_block,
                   size_t keys_count,
                   size_t subkeys_count,
                   std::shared_ptr<Behavior> behavior) noexcept
    : header_block_{&header_block},
      behavior_{std::move(behavior)},
      version_{version},
      keys_count_{keys_count},
      subkeys_count_{subkeys_count} {}

Snapshot::Snapshot() noexcept = default;

Snapshot::Snapshot(Snapshot&& other) noexcept
    : header_block_{other.header_block_},
      behavior_{std::move(other.behavior_)},
      version_{other.version_},
      keys_count_{other.keys_count_},
      subkeys_count_{other.subkeys_count_} {
  other.header_block_ = nullptr;
  other.version_ = 0;
  other.keys_count_ = 0;
  other.subkeys_count_ = 0;
}

Snapshot::Snapshot(const Snapshot& other) noexcept
    : header_block_{other.header_block_},
      behavior_{other.behavior_},
      version_{other.version_},
      keys_count_{other.keys_count_},
      subkeys_count_{other.subkeys_count_} {
  if (header_block_)
    header_block_->AddSnapshotReference(version_);
}

Snapshot& Snapshot::operator=(Snapshot&& other) noexcept {
  if (this != &other) {
    if (header_block_)
      header_block_->RemoveSnapshotReference(version_, *behavior_);

    header_block_ = other.header_block_;
    behavior_ = std::move(other.behavior_);
    version_ = other.version_;
    keys_count_ = other.keys_count_;
    subkeys_count_ = other.subkeys_count_;
    other.header_block_ = nullptr;
    other.version_ = 0;
    other.keys_count_ = 0;
    other.subkeys_count_ = 0;
  }
  return *this;
}

Snapshot& Snapshot::operator=(const Snapshot& other) noexcept {
  if (this != &other) {
    if (other.header_block_)
      header_block_->AddSnapshotReference(version_);

    if (header_block_)
      header_block_->RemoveSnapshotReference(version_, *behavior_);

    header_block_ = other.header_block_;
    behavior_ = std::move(other.behavior_);
    version_ = other.version_;
    keys_count_ = other.keys_count_;
    subkeys_count_ = other.subkeys_count_;
  }
  return *this;
}

VersionedPayloadHandle Snapshot::Get(const KeyDescriptor& key,
                                     uint64_t subkey) const noexcept {
  if (header_block_) {
    if (Detail::SubkeyStateView view =
            Detail::BlobAccessor{
                const_cast<Detail::HeaderBlock&>(*header_block_)}
                .FindSubkeyState(key, subkey)) {
      return view.GetPayload(version_);
    }
  }
  return {};
}

std::optional<KeyView> Snapshot::Get(const KeyDescriptor& key) const noexcept {
  Detail::BlobAccessor accessor{
      const_cast<Detail::HeaderBlock&>(*header_block_)};

  if (header_block_) {
    Detail::KeyStateView key_state_view =
        Detail::BlobAccessor{const_cast<Detail::HeaderBlock&>(*header_block_)}
            .FindKeyState(key);

    if (key_state_view) {
      if (auto subkeys_count =
              key_state_view.GetSubkeysCount(Detail::MakeVersionOffset(
                  version_, header_block_->base_version()))) {
        return KeyView{
            subkeys_count,
            key_state_view.state_block_,
        };
      }
    }
  }
  return {};
}

size_t Snapshot::GetSubkeysCount(const KeyDescriptor& key) const noexcept {
  if (header_block_) {
    if (Detail::KeyStateView view =
            Detail::BlobAccessor{*header_block_}.FindKeyState(key)) {
      return view.GetSubkeysCount(
          Detail::MakeVersionOffset(version_, header_block_->base_version()));
    }
  }
  return 0;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
