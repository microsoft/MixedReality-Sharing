// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include "src/layout.h"

#include <atomic>
#include <optional>
#include <vector>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// If the StateBlock for a subkey runs out of space to store versions,
// one or more consecutive SubkeyVersionBlocks are allocated.
// The first block holds up to 4 versions, the following blocks hold up to 5
// each ("up to" because the versions are compressed relative to the first
// version in the block; so if two versions are too far apart, the second
// version will be moved to the next block, possibly wasting some capacity).
//
// Sequences of SubkeyVersionBlocks (consisting of 1 or more blocks, as
// described above) allocated for the same subkey form a stack. Only the top of
// the stack is visible to the readers, but during the deallocation all elements
// of it are traversed to release the versioned payloads.
// Duplicates of the payloads (between the sequences of SubkeyVersionBlocks and
// the SubkeyStateBlock, which contains up to two of them) are only released
// once (and we don't add a reference to a payload when duplicating it during
// the reallocation of a SubkeyVersionBlock within the same blob).

class alignas(kBlockSize) SubkeyVersionBlock {
 public:
  using PayloadType = VersionedPayloadHandle;

  // Returns the payload associated with the version, and the actual
  // version when it was inserted. Will return a VersionedPayloadHandle
  // without a payload if it doesn't exist in the version.
  VersionedPayloadHandle GetVersionedPayload(uint64_t version) const noexcept;

  VersionedPayloadHandle latest_versioned_payload_thread_unsafe() const
      noexcept;

  // Returns true if either the payload or the deletion marker of the
  // specified version can be published. Should only be called by the
  // writer thread, and only if the version is greater than all versions
  // pushed before that (the behavior is undefined if the provided version
  // is not greater than existing versions).
  bool CanPushFromWriterThread(uint64_t version, bool has_payload) const
      noexcept;

  // Should only be called if the payload doesn't match the latest payload.
  void PushFromWriterThread(uint64_t version,
                            std::optional<PayloadHandle> payload) noexcept;

  class Builder {
   public:
    // Constructs uninitialized_first_block and increments
    // stored_data_blocks_count (it will also be incremented each time a
    // Push operation would require an extra block).
    // avaliable_blocks_count must be at least 1, and the total number
    // of written blocks won't exceed it.
    Builder(DataBlockLocation previous,
            SubkeyVersionBlock& uninitialized_first_block,
            uint32_t avaliable_blocks_count,
            uint32_t& stored_data_blocks_count) noexcept;

    // Attempts to store the payload, allocating a new block if
    // necessary. The operation will succeed with no effect if no
    // changes are required (if provided VersionedPayloadHandle is
    // already stored, or if it has no payload while the latest version
    // also has no payload).
    //
    // version: the version for which the payload was retrieved from another
    // block.
    // observed_payload_for_version: the result of GetVersionedPayload() call
    // for version. Can be invalid.
    //
    // If observed_payload_for_version has a payload, argument 'version' is
    // ignored (the builder will store the version obtained from
    // observed_payload_for_version).
    //
    // If observed_payload_for_version has no payload, the deletion marker will
    // be stored for the provided 'version' argument.
    //
    // The difference in the behavior is to make the synchronization between two
    // storages easier. All existing subkeys will always keep the exact version
    // when they were inserted (so the synchronization can check the versions of
    // subkeys without checking their content to know for sure that they are
    // identical). At the same time, all removed subkeys will be just "missing"
    // (with no way to know since when they are missing). This ensures that
    // deleted subkeys are not imposing a permanent tax on the storage, and the
    // storage is allowed to forget about them during the reallocation.
    bool Push(uint64_t version,
              VersionedPayloadHandle observed_payload_for_version) noexcept;

    // Finalizes the construction by reserving at least one extra slot (capable
    // of holding the provided version) and finalizing the first block.
    // Generally tries to reserve enough space to keep the initial load factor
    // around 0.5, but will simply reserve as much as it can if there are not
    // enough free blocks.
    // Returns false if the builder failed to reserve the free slot for the
    // provided version (blocks already consumed by the builder will be in
    // uspecified state; the caller is expected to abandon them and reallocate
    // the entire storage blob).
    bool FinalizeAndReserveOne(uint64_t version, bool has_payload) noexcept;

   private:
    SubkeyVersionBlock& first_block_;
    uint32_t avaliable_blocks_count_;
    uint32_t& stored_data_blocks_count_;
    uint32_t size_ = 0;
    uint32_t capacity_ = 4;
    uint32_t current_block_size_ = 0;
    uint32_t current_block_capacity_ = 4;
    SubkeyVersionBlock* current_block_ = &first_block_;
    VersionedPayloadHandle latest_payload_;
  };

  // Appends all payloads (in unspecified order) to the result.
  // The existing values are not cleared.
  // Returns the location of the previous block in the list of blocks.
  //
  // TODO: instead of gathering all payloads in a vector, we could just iterate
  // over all payloads starting from the end of the previous version block.
  // This way we won't encounter any duplicates and won't have to make an array.
  DataBlockLocation AppendPayloads(
      std::vector<VersionedPayloadHandle>& result) const noexcept;

  // For testing.
  uint32_t size_relaxed() const noexcept {
    return size_.load(std::memory_order_relaxed);
  }

  // For testing.
  uint32_t capacity() const noexcept { return capacity_; }

  constexpr uint64_t first_marked_version_in_block() const noexcept {
    return first_marked_version_in_block_;
  }

 private:
  // Uninitialized fields are handled by the Builder.
  explicit SubkeyVersionBlock(DataBlockLocation previous) noexcept
      : previous_{previous} {}

  static constexpr uint32_t kInvalidMarkedOffset = ~0u;

  // Bit 0 stores the deletion marker, and the remaining 63 bits store
  // the version. Initialized by the builder and Push operations.
  uint64_t first_marked_version_in_block_;

  // Adding these values to first_marked_version_in_block_ will give a
  // "version + deletion marker" value encoded the same way as
  // first_marked_version_in_block_ above.
  // In the majority of the cases all versions in this block will be
  // compressible in this way.
  // In very rare cases where the distance between the versions is too
  // large, (which can happen if we are modifying a value that was set
  // ~2 billions of versions ago), the offsets will hold the invalid
  // value, effectively creating holes in the data structure (the
  // version we couldn't compress will become a
  // first_marked_version_in_block_ of the next block). Note that
  // marked_offsets_[N] corresponds to payloads_[N + 1].
  uint32_t marked_offsets_[3];

  // First block uses capacity_, all other blocks use
  // last_marked_offset_, threating the array above as if it was 1
  // element larger.
  union {
    uint32_t last_marked_offset_;
    uint32_t capacity_;
  };

  PayloadHandle payloads_[4];

  // First block uses previous_ and size_, all other blocks use
  // last_payload_, threating the array above as if it was 1 element
  // larger.
  union {
    PayloadHandle last_payload_;
    struct {
      // All sequences of these blocks form a linked list, which is
      // traversed when we destroy the blob to release the payloads.
      const DataBlockLocation previous_;

      // Index of the first unused payload in this sequence of blocks.
      // Note that it doesn't have to be equal to the number of actually
      // stored payloads, since certain payloads could be skipped (and
      // corresponding VersionOffset for them would be kInvalid).
      std::atomic_uint32_t size_;
    };
  };
};

static_assert(sizeof(SubkeyVersionBlock) == kBlockSize);

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
