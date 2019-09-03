// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include <cassert>
#include <cstdint>
#include <optional>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// The storage consists of storage blobs.
// Each blob is a page-aligned, and consists of small blocks
// (each block is 64 bytes large).
// The layout of the blob is:
// * Header block [1 block]
// * Index blocks [2^k blocks]
// * Data blocks
//   - Blocks with reference counts for versions stored in the blob  (allocated
//     from the beginning of the data blocks area).
//   - State blocks and version blocks (allocated from the end of the data
//     blocks area). They are referenced by index blocks and each other.
// Note that initially it is unknown how many data blocks will be used for
// various purposes. The blocks are simply allocated (from both ends) until the
// blob runs out of space.
// After that, a new blob has to be allocated. The old one will stay alive while
// there are references to it.

constexpr uint32_t kBlockSize = 64;

enum class IndexLevel {
  Key,
  Subkey,
};

// References a slots in the index section of the blob
// (each block has several slots).
enum class IndexSlotLocation : uint32_t { kInvalid = ~0u };

// References blocks in the data section
// (both the state blocks and the version blocks).
enum class DataBlockLocation : uint32_t { kInvalid = ~0u };

static constexpr uint64_t kSmallestInvalidVersion = 0x7FFF'FFFF'FFFF'FFFF;

class VersionedPayloadHandle {
 public:
  VersionedPayloadHandle() = default;
  VersionedPayloadHandle(uint64_t version, PayloadHandle payload)
      : version_{version}, payload_{payload} {}

  constexpr bool has_payload() const noexcept {
    return version_ < kSmallestInvalidVersion;
  }

  uint64_t version() const noexcept {
    assert(has_payload());
    return version_;
  }

  PayloadHandle payload() const noexcept {
    assert(has_payload());
    return payload_;
  }

  constexpr bool operator==(const VersionedPayloadHandle& other) const
      noexcept {
    return version_ == other.version_ && payload_ == other.payload_;
  }

  constexpr bool operator!=(const VersionedPayloadHandle& other) const
      noexcept {
    return version_ != other.version_ || payload_ != other.payload_;
  }

 private:
  uint64_t version_{kSmallestInvalidVersion};
  PayloadHandle payload_{0};
};

// Three-state value of a payload handle, useful for requirements etc.
class OptionalPayloadStateOrDeletionMarker {
 public:
  constexpr OptionalPayloadStateOrDeletionMarker() = default;
  constexpr OptionalPayloadStateOrDeletionMarker(nullptr_t)
      : state_{State::DeletionMarker} {}
  constexpr OptionalPayloadStateOrDeletionMarker(PayloadHandle handle)
      : state_{State::SpecificHandle}, handle_{handle} {}

  operator bool() const noexcept { return state_ != State::NoState; }

  constexpr bool operator!() const noexcept { return state_ == State::NoState; }
  constexpr bool is_specific_handle() const noexcept {
    return state_ == State::SpecificHandle;
  }
  constexpr bool is_deletion_marker() const noexcept {
    return state_ == State::DeletionMarker;
  }

  PayloadHandle operator*() const noexcept {
    assert(state_ == State::SpecificHandle);
    return handle_;
  }

  [[nodiscard]] std::optional<PayloadHandle> release() noexcept {
    assert(state_ != State::NoState);
    if (state_ == State::SpecificHandle) {
      state_ = State::NoState;
      return handle_;
    }
    state_ = State::NoState;
    return {};
  }

 private:
  enum class State {
    NoState,
    DeletionMarker,
    SpecificHandle,
  };

  State state_{State::NoState};
  PayloadHandle handle_{0};  // Irrelevant unless state_ is SpecificHandle
};

class KeyStateBlock;
class KeyVersionBlock;
class SubkeyStateBlock;
class SubkeyVersionBlock;
class KeyBlockStateSearchResult;
class SubkeyBlockStateSearchResult;

namespace Detail {
template <IndexLevel>
struct Types;

template <>
struct Types<IndexLevel::Key> {
  using ValueType = uint32_t;
  using StateBlockType = KeyStateBlock;
  using VersionBlockType = KeyVersionBlock;
  using BlockStateSearchResultType = KeyBlockStateSearchResult;
};

template <>
struct Types<IndexLevel::Subkey> {
  using ValueType = VersionedPayloadHandle;
  using StateBlockType = SubkeyStateBlock;
  using VersionBlockType = SubkeyVersionBlock;
  using BlockStateSearchResultType = SubkeyBlockStateSearchResult;
};
}  // namespace Detail

template <IndexLevel kLevel>
using ValueType = typename Detail::Types<kLevel>::ValueType;
template <IndexLevel kLevel>
using StateBlock = typename Detail::Types<kLevel>::StateBlockType;
template <IndexLevel kLevel>
using VersionBlock = typename Detail::Types<kLevel>::VersionBlockType;
template <IndexLevel kLevel>
using BlockStateSearchResult =
    typename Detail::Types<kLevel>::BlockStateSearchResultType;

// FIXME: remove
template <typename TBlock>
inline TBlock& GetBlockAt(std::byte* data_begin,
                          DataBlockLocation location) noexcept {
  assert(location != DataBlockLocation::kInvalid);
  return *reinterpret_cast<TBlock*>(data_begin +
                                    static_cast<size_t>(location) * kBlockSize);
}

// FIXME: remove
template <typename TBlock>
inline const TBlock& GetBlockAt(const std::byte* data_begin,
                                DataBlockLocation location) noexcept {
  assert(location != DataBlockLocation::kInvalid);
  return *reinterpret_cast<const TBlock*>(
      data_begin + static_cast<size_t>(location) * kBlockSize);
}

// An small offset from some base version.
enum class VersionOffset : uint32_t { kInvalid = ~0u };

constexpr bool IsVersionConvertibleToOffset(uint64_t version,
                                            uint64_t base_version) noexcept {
  return version >= base_version &&
         version - base_version <
             static_cast<uint64_t>(VersionOffset::kInvalid);
}

inline VersionOffset MakeVersionOffset(uint64_t version,
                                       uint64_t base_version) noexcept {
  assert(IsVersionConvertibleToOffset(version, base_version));
  return VersionOffset{static_cast<uint32_t>(version - base_version)};
}

struct VersionedSubkeysCount {
  VersionOffset version_offset;  // Relative to the base version of the blob.
  uint32_t subkeys_count;
};

constexpr bool operator<(const VersionedSubkeysCount& versioned_count,
                         VersionOffset version_offset) noexcept {
  return versioned_count.version_offset < version_offset;
}

constexpr bool operator<(
    VersionOffset version_offset,
    const VersionedSubkeysCount& versioned_count) noexcept {
  return version_offset < versioned_count.version_offset;
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
