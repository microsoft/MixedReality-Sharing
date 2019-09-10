// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/SubkeyIterator.h>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {
namespace Detail {
class KeyStateBlock;
class HeaderBlock;
}  // namespace Detail

class KeyIterator;

class KeyView {
 public:
  KeyView(uint64_t observed_version,
          size_t subkeys_count,
          Detail::KeyStateBlock* key_state_block,
          Detail::IndexBlock* index_begin,
          std::byte* data_begin) noexcept
      : observed_version_{observed_version},
        subkeys_count_{subkeys_count},
        key_state_block_{key_state_block},
        index_begin_{index_begin},
        data_begin_{data_begin} {}

  // Returns a non-owning view (valid for as long as the snapshot is alive).
  KeyHandle key_handle() const noexcept;

  // Returns the version for which this view is accurate.
  uint64_t observed_version() const noexcept { return observed_version_; }

  // Returns the number of subkeys for this key (for the observed version).
  uint64_t subkeys_count() const noexcept { return subkeys_count_; }

  SubkeyIterator begin() const noexcept;
  SubkeyIteratorEnd end() const noexcept { return {}; }

 private:
  // Finds the first key view in the header block that has more than 0 subkeys
  // in the observed version. Used only by the KeyIterator.
  KeyView(uint64_t observed_version,
          Detail::VersionOffset version_offset,
          Detail::HeaderBlock& header_block) noexcept;

  // Advances to the next key view that has more than 0 subkeys
  // in the observed version. Used only by the KeyIterator.
  void Advance(Detail::VersionOffset version_offset) noexcept;

  void AdvanceUntilSubkeysFound(Detail::IndexSlotLocation location,
                                Detail::VersionOffset version_offset) noexcept;

  uint64_t observed_version_ = Detail::kSmallestInvalidVersion;
  size_t subkeys_count_ = 0;
  Detail::KeyStateBlock* key_state_block_ = nullptr;
  Detail::IndexBlock* index_begin_ = nullptr;
  std::byte* data_begin_ = nullptr;

  friend class KeyIterator;
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
