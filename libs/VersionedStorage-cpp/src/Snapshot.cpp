// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Snapshot.h>

#include <Microsoft/MixedReality/Sharing/VersionedStorage/KeyEnumerator.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyEnumerator.h>

#include "src/HeaderBlock.h"
#include "src/StateBlock.h"
#include "src/StateBlockEnumerator.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace {
// class EmptySubkeyEnumeratorImpl : public SubkeyEnumerator {
//  public:
//   bool MoveNext() noexcept override { return false; }
//   void Reset() noexcept override {}
// };

// class EmptyKeyEnumeratorImpl : public KeyEnumerator {
//  public:
//   std::unique_ptr<SubkeyEnumerator> CreateSubkeyEnumerator() const
//       noexcept override {
//     assert(false);
//     return {};
//   }
//
//   bool MoveNext() noexcept override { return false; }
//   void Reset() noexcept override {}
// };

class SubkeyEnumeratorImpl : public SubkeyEnumerator {
 public:
  SubkeyEnumeratorImpl(std::shared_ptr<const Snapshot> snapshot,
                       SubkeyStateBlockEnumerator&& block_enumerator)
      : snapshot_{std::move(snapshot)},
        block_enumerator_{std::move(block_enumerator)} {}

  bool MoveNext() noexcept override {
    while (block_enumerator_.MoveNext()) {
      VersionedPayloadHandle payload =
          block_enumerator_.GetPayload(snapshot_->version());
      if (payload.has_payload()) {
        current_subkey_ = block_enumerator_.CurrentStateBlock().subkey_;
        current_payload_handle_ = payload.payload();
        has_current_ = true;
        return true;
      }
    }
    return false;
  }

  void Reset() noexcept override {
    block_enumerator_.Reset();
    has_current_ = false;
  }

 private:
  std::shared_ptr<const Snapshot> snapshot_;
  SubkeyStateBlockEnumerator block_enumerator_;
};

class KeyEnumeratorImpl : public KeyEnumerator {
 public:
  KeyEnumeratorImpl(std::shared_ptr<const Snapshot> snapshot,
                    KeyStateBlockEnumerator&& block_enumerator)
      : snapshot_{std::move(snapshot)},
        block_enumerator_{std::move(block_enumerator)} {}

  std::unique_ptr<SubkeyEnumerator> CreateSubkeyEnumerator() const
      noexcept override {
    assert(has_current_);
    return std::make_unique<SubkeyEnumeratorImpl>(
        snapshot_, block_enumerator_.CreateSubkeyStateBlockEnumerator());
  }

  bool MoveNext() noexcept override {
    while (block_enumerator_.MoveNext()) {
      if (auto count =
              block_enumerator_.GetSubkeysCount(snapshot_->version())) {
        current_subkeys_count_ = count;
        current_key_ = block_enumerator_.CurrentStateBlock().key_;
        has_current_ = true;
        return true;
      }
    }
    return false;
  }

  void Reset() noexcept override {
    block_enumerator_.Reset();
    has_current_ = false;
  }

 private:
  std::shared_ptr<const Snapshot> snapshot_;
  KeyStateBlockEnumerator block_enumerator_;
};

}  // namespace

Snapshot::Snapshot(uint64_t version,
                   HeaderBlock& header_block,
                   std::shared_ptr<Behavior> behavior) noexcept
    : header_block_{header_block},
      behavior_{std::move(behavior)},
      version_{version},
      keys_count_{header_block.keys_count()},
      subkeys_count_{header_block.subkeys_count()} {}

Snapshot::~Snapshot() noexcept {
  header_block_.RemoveSnapshotReference(version_, *behavior_);
}

size_t Snapshot::GetSubkeysCount(const AbstractKey& key) const noexcept {
  return HeaderBlock::Accessor{const_cast<HeaderBlock&>(header_block_)}
      .FindKey(version_, key)
      .value();
}

std::optional<PayloadHandle> Snapshot::Get(const AbstractKey& key,
                                           uint64_t subkey) const noexcept {
  VersionedPayloadHandle handle =
      HeaderBlock::Accessor{const_cast<HeaderBlock&>(header_block_)}
          .FindSubkey(version_, key, subkey)
          .value();
  if (handle.has_payload())
    return handle.payload();

  return {};
}

std::unique_ptr<KeyEnumerator> Snapshot::CreateKeyEnumerator() const noexcept {
  return std::make_unique<KeyEnumeratorImpl>(
      shared_from_this(),
      HeaderBlock::Accessor(header_block_).CreateKeyStateBlockEnumerator());
}

std::unique_ptr<SubkeyEnumerator> Snapshot::CreateSubkeyEnumerator(
    const AbstractKey& key) const noexcept {
  return std::make_unique<SubkeyEnumeratorImpl>(
      shared_from_this(), HeaderBlock::Accessor(header_block_)
                              .CreateSubkeyStateBlockEnumerator(key));
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
