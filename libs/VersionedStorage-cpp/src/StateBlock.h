// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/enums.h>

#include "src/layout.h"

#include <atomic>
#include <cassert>
#include <vector>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// Base class of the state of either a key or a subkey belonging to a key.
// Each block is participating in a hash collection, a linked list and a tree.
// See KeyStateBlock and SubkeyStateBlock below for detailed explanations.
class StateBlockBase {
 public:
  StateBlockBase(KeyHandle key,
                 uint64_t subscription_handle,
                 uint32_t inplace_versions_count_or_version_offset) noexcept
      : key_{key},
        subscription_and_tree_height_{subscription_handle},
        left_tree_child_{DataBlockLocation::kInvalid},
        right_tree_child_{DataBlockLocation::kInvalid},
        inplace_versions_count_or_version_offset_{
            inplace_versions_count_or_version_offset} {
    // Expecting the top 8 bits of the subscription to be 0, so that we can use
    // them to store the tree level.
    assert(subscription_handle < kSubscriptionMask);
  }

  // Returns the level of the block in the AA-tree of blocks.
  // The level of leaves is 0. Note that levels of AA-tree are defined in a way
  // so that a node and its parent may belong to the same level. Refer to
  // AA-tree documentation for details.
  constexpr uint8_t tree_level() const noexcept {
    return subscription_and_tree_height_ >> kTreeHeightShiftBits;
  }

  void IncrementTreeLevel() noexcept {
    subscription_and_tree_height_ += kTreeHeightIncrement;
  }

  constexpr bool is_scratch_buffer_mode() const noexcept {
    return (subscription_and_tree_height_ & kScratchBufferModeMask) ==
           kScratchBufferModeMask;
  }

  void SetScratchBuffer(void* scratch_buffer) noexcept {
    subscription_and_tree_height_ |= kScratchBufferModeMask;
    writer_thread_scratch_buffer_ = scratch_buffer;
  }

  void* GetScratchBuffer() const noexcept {
    assert(is_scratch_buffer_mode());
    return writer_thread_scratch_buffer_;
  }

  const KeyHandle key_;

  uint64_t subscription_and_tree_height_;
  union {
    struct {
      // Left child in the tree of blocks (used for quick block insertion).
      DataBlockLocation left_tree_child_;
      // Right child in the tree of blocks (used for quick block insertion).
      DataBlockLocation right_tree_child_;
    };

    // If the blob had ran out of capacity and is being reallocated,
    // the AA-tree of children is no longer useful.
    // The memory previously used by the children locations can be used by the
    // writer thread to speed the reallocation up.
    // The writer thread will be using the tree height bits to tell which mode
    // this node is in, see is_scratch_buffer_mode()/SetScratchBuffer().
    void* writer_thread_scratch_buffer_;
  };

  // Location of the next key or subkey in the iteration order.
  std::atomic<IndexSlotLocation> next_{IndexSlotLocation::kInvalid};

  // For keys, holds the number of in-place subkeys counts.
  // (between 0 and 3).
  // For subkeys, stores the difference between the marked versions
  // of payloads_[1] and payloads_[0].
  std::atomic<uint32_t> inplace_versions_count_or_version_offset_;

 protected:
  static constexpr uint64_t kTreeHeightShiftBits = 56;
  static constexpr uint64_t kTreeHeightIncrement = 1ull << kTreeHeightShiftBits;
  static constexpr uint64_t kSubscriptionMask = kTreeHeightIncrement - 1;
  static constexpr uint64_t kScratchBufferModeMask = ~kSubscriptionMask;
};

static_assert(sizeof(StateBlockBase) == kBlockSize / 2);

// Key state block always owns the key and contains up to 3 versions for the
// number of subkeys in the block.
//
// The location of each key state block is saved in some index block (for quick
// searches), right next to the location of the most recent KeyVersionBlock
// (which is used to store subkey counts for versions when there is no more
// space in KeyStateBlock).
//
// All key state blocks form a thread-safe insert-only sorted linked list. Head
// of this list is located in the HeaderBlock of the same blob where
// KeyStateBlock is allocated. This list is used by readers for fast thread-safe
// iteration over all keys (and additional filtering is applied to skip the
// blocks that did not exist in the version that is being iterated over).
//
// To make the insertion into the linked list fast, all key state blocks form a
// binary AA-tree, with a root located in the HeaderBlock. The tree is not
// thread-safe, but only accessed by the writer thread (to find the "previous"
// block according to the sorting predicate defined in Behavior), so this tree
// is not exposed through higher-level interfaces and is only accessed from
// under the lock.
//
// Each KeyState block contains a head of the sorted linked list of all the
// subkey blocks associated with this block, as well as the root of the AA-tree
// tree of the subkeys (their meaning is similar to the list and the tree of key
// blocks, which are described above). See SubkeyStateBlock below for more
// details.
class alignas(kBlockSize) KeyStateBlock : public StateBlockBase {
 public:
  KeyStateBlock(KeyHandle key, KeySubscriptionHandle subscription) noexcept
      : StateBlockBase{key, static_cast<uint64_t>(subscription), 0} {}

  constexpr KeySubscriptionHandle subscription() const noexcept {
    return KeySubscriptionHandle{subscription_and_tree_height_ &
                                 kSubscriptionMask};
  }

  constexpr bool has_subscription() const noexcept {
    return subscription() != KeySubscriptionHandle::kInvalid;
  }

  // Returns the number of subkeys in this version.
  uint32_t GetSubkeysCount(VersionOffset version_offset) const noexcept;

  // Should only be called by the writer thread.
  uint32_t GetLatestSubkeysCount() const noexcept;

  void PushSubkeysCount(VersionOffset version_offset,
                        uint32_t subkeys_count) noexcept;

  // Called by the writer thread only.
  bool HasFreeInPlaceSlots() const noexcept {
    const auto inplace_versions_count =
        inplace_versions_count_or_version_offset_.load(
            std::memory_order_relaxed);
    return inplace_versions_count < 3;
  }

  // Intentionally uninitialized.
  // These variables are not accessed unless a version has been pushed.
  VersionedSubkeysCount inplace_payloads_[3];

  // Atomic append-only list of subkeys associated with this key.
  // Note that some of the subkeys may be missing from specific versions,
  // so the iteration will filter out this list.
  std::atomic<IndexSlotLocation> subkeys_list_head_{
      IndexSlotLocation::kInvalid};

  // The root of the AA-tree of subkeys used by the writer thread
  // for fast insertion into the list of subkeys.
  DataBlockLocation subkeys_tree_root_{DataBlockLocation::kInvalid};
};

static_assert(sizeof(KeyStateBlock) == kBlockSize);

// Subkey state block store up to two versions of payloads associated with
// subkeys.
//
// The location of each subkey state block is saved in some index block (for
// quick searches), right next to the location of the most recent
// SubkeyVersionBlock (which is used to store subkey counts for versions when
// there is no more space in SubkeyStateBlock).
//
// SubkeyStateBlock never owns the key associated with it (it is owned by
// KeyStateBlock), and each SubkeyStateBlock always logically belongs to some
// KeyStateBlock. All SubkeyStateBlocks related to the same key form a
// thread-safe insert-only sorted linked list. Head of this list is located in
// the owning KeyStateBlob within the same blob where SubkeyStateBlock is
// allocated. This list is used by readers for fast thread-safe iteration over
// all subkeys withing a key (and additional filtering is applied to skip the
// subkeys that did not exist in the version that is being iterated over).
//
// To make the insertion into the linked list fast, all subkey state blocks form
// a binary AA-tree, with a root located in the owning KeyStateBlock. The tree
// is not thread-safe, but only accessed by the writer thread (to find the
// "previous" block according to the ordering of subkeys), so this tree is not
// exposed through higher-level interfaces and is only accessed from under the
// lock.
//
// SubkeyStateBlock and all SubkeyVersionBlocks associated with it implicitly
// share the ownership of the versioned payloads. If a versioned payload was
// owned by the SubkeyStateBlock, and then was copied into one or more
// SubkeyVersionBlocks, no additional references will be added to the payload,
// and during the deallocation the reference will only be released once.
class alignas(kBlockSize) SubkeyStateBlock : public StateBlockBase {
 public:
  SubkeyStateBlock(KeyHandle key,
                   SubkeySubscriptionHandle subscription,
                   uint64_t subkey) noexcept
      : StateBlockBase{key, static_cast<uint64_t>(subscription),
                       kInvalidMarkedOffset},
        subkey_{subkey} {}

  constexpr SubkeySubscriptionHandle subscription() const noexcept {
    return SubkeySubscriptionHandle{subscription_and_tree_height_ &
                                    kSubscriptionMask};
  }

  constexpr bool has_subscription() const noexcept {
    return subscription() != SubkeySubscriptionHandle::kInvalid;
  }

  VersionedPayloadHandle GetVersionedPayload(uint64_t version) const noexcept;

  // Should only be called by the writer thread
  VersionedPayloadHandle GetLatestVersionedPayload() const noexcept;

  std::vector<VersionedPayloadHandle> GetAllPayloads() const noexcept;

  // Called by the writer thread only.
  bool CanPush(uint64_t version, bool has_payload) const noexcept {
    const auto v0 = marked_version_0_.load(std::memory_order_relaxed);
    if (v0 == kInvalidMarkedVersion)
      return true;

    const uint64_t marked_version =
        (version << 1) | static_cast<uint64_t>(!has_payload);

    return ((marked_version - v0) < kInvalidMarkedOffset) &&
           (inplace_versions_count_or_version_offset_.load(
                std::memory_order_relaxed) == kInvalidMarkedOffset);
  }

  void Push(uint64_t version, std::optional<PayloadHandle> payload) noexcept;

  static constexpr uint64_t kInvalidMarkedVersion = ~0ull;
  static constexpr uint32_t kInvalidMarkedOffset = ~0u;

  std::atomic<uint64_t> marked_version_0_{kInvalidMarkedVersion};
  // Payloads are modified at most once during the lifetime of this block,
  // and they will never be used before they are initialized, and then either
  // version_0_ or version_offset_ from the base class are published.
  // The initial state is intentionally uninitialized.
  PayloadHandle payloads_[2];
  const uint64_t subkey_;
};

static_assert(sizeof(SubkeyStateBlock) == kBlockSize);

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
