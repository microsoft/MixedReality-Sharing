// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>

#include "src/HeaderBlock.h"
#include "src/StateBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

Snapshot::~Snapshot() noexcept {
  if (header_block_)
    header_block_->RemoveSnapshotReference(info_.version_, *behavior_);
}

Snapshot::Snapshot(Detail::HeaderBlock& header_block,
                   std::shared_ptr<Behavior> behavior,
                   const SnapshotInfo& info) noexcept
    : header_block_{&header_block},
      behavior_{std::move(behavior)},
      info_{info} {}

Snapshot::Snapshot() noexcept = default;

Snapshot::Snapshot(Snapshot&& other) noexcept
    : header_block_{other.header_block_},
      behavior_{std::move(other.behavior_)},
      info_{other.info_} {
  other.header_block_ = nullptr;
  other.info_ = {};
}

Snapshot::Snapshot(const Snapshot& other) noexcept
    : header_block_{other.header_block_},
      behavior_{other.behavior_},
      info_{other.info_} {
  if (header_block_)
    header_block_->AddSnapshotReference(info_.version_);
}

Snapshot& Snapshot::operator=(Snapshot&& other) noexcept {
  if (this != &other) {
    if (header_block_)
      header_block_->RemoveSnapshotReference(info_.version_, *behavior_);

    header_block_ = other.header_block_;
    behavior_ = std::move(other.behavior_);
    info_ = other.info_;
    other.header_block_ = nullptr;
    other.info_ = {};
  }
  return *this;
}

Snapshot& Snapshot::operator=(const Snapshot& other) noexcept {
  if (this != &other) {
    if (other.header_block_)
      header_block_->AddSnapshotReference(other.info_.version_);

    if (header_block_)
      header_block_->RemoveSnapshotReference(info_.version_, *behavior_);

    header_block_ = other.header_block_;
    behavior_ = std::move(other.behavior_);
    info_ = other.info_;
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
      return view.GetPayload(info_.version_);
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
                  info_.version_, header_block_->base_version()))) {
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
      return view.GetSubkeysCount(Detail::MakeVersionOffset(
          info_.version_, header_block_->base_version()));
    }
  }
  return 0;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
