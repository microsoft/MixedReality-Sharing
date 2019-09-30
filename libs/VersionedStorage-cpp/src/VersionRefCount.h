// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include "src/layout.h"

#include <atomic>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

// Reference counters for versions associated with a blob are located at the end
// of each blob, in the reverse order.
// The last 4 bytes of the blob will store the refcount of the base version,
// previous 4 bytes will store the refcount for the next version, etc.
//
// When the writer thread which iterates over all alive versions encounters
// more than one empty version in a row, it switches the first one to jump mode
// by setting the lowest bit to 1 (normally it stays at 0 and is not affected by
// the refcount, which works in increments of 2).
// In this mode the other bits are storing the jump distance: the number of
// VersionRefCount objects with refcount 0 that can be safely skipped.
// The jump distance is updated as more versions become unreferenced, thus
// reducing the amortized complexity of the iteration process to
// O(alive_versions_count).

class VersionRefCount {
 public:
  class Accessor {
   public:
    Accessor(VersionRefCount* refcount_of_base_version) noexcept
        : refcount_of_base_version_{refcount_of_base_version} {}

    void InitVersion(VersionOffset offset) {
      new (refcount_of_base_version_ - static_cast<size_t>(offset))
          VersionRefCount;
    }

    void AddReference(VersionOffset offset) {
      const uint32_t old_value =
          (refcount_of_base_version_ - static_cast<size_t>(offset))
              ->value_.fetch_add(2, std::memory_order_relaxed);
      assert((old_value & 1) == 1 && old_value >= 3);
    }

    // Returns true if the reference count is 0
    bool RemoveReference(VersionOffset offset) {
      uint32_t old_value =
          (refcount_of_base_version_ - static_cast<size_t>(offset))
              ->value_.fetch_sub(2, std::memory_order_acq_rel);
      assert((old_value & 1) == 1 && old_value >= 3);
      return old_value == 3;
    }

    // TFunc is bool(VersionOffset) and it returns true
    // if the iteration should stop.
    template <typename TFunc>
    bool ForEachAliveVersion(uint32_t versions_count, TFunc&& func) noexcept {
      VersionRefCount* jump_start = nullptr;
      uint32_t jump_distance = 0;
      for (uint32_t i = 0; i < versions_count;) {
        VersionRefCount& rc = *(refcount_of_base_version_ - i);
        auto snapshot = rc.value_.load(std::memory_order_relaxed);
        const bool is_refcount_mode = (snapshot & 1) != 0;
        if (is_refcount_mode && snapshot > 1) {
          // The version is alive
          if (func(VersionOffset{i}))
            return true;
          jump_start = nullptr;
          ++i;
        } else if (jump_start) {
          // We just jumped here from an unreferenced version and discovered
          // that this version is also unreferenced.
          // Updating the jump distance to never end up here again, keeping the
          // amortized complexity of the iteration at O(alive_versions_count).

          // If this is an unreferenced version in refcount mode, the value of
          // the snapshot is 1, so jump_increment will end up being 1.
          // Otherwise if this is in jump mode, jump_increment will be equal to
          // the correct jump distance.
          const uint32_t jump_increment = (snapshot + 1) >> 1;
          jump_distance += jump_increment;
          jump_start->value_.store(jump_distance << 1,
                                   std::memory_order_relaxed);
          i += jump_increment;
        } else {
          // This version should be skipped, and this is the first unreferenced
          // version in a row that we checked.
          jump_start = &rc;

          // If this is an unreferenced version in refcount mode, the value of
          // the snapshot is 1, so jump_distance will end up being 1.
          // Otherwise if this is in jump mode, jump_distance will be equal to
          // the correct jump distance.
          jump_distance = (snapshot + 1) >> 1;
          i += jump_distance;
        }
      }
      return false;
    }

   private:
    VersionRefCount* const refcount_of_base_version_;
  };

  static constexpr uint32_t kCountsPerBlock = kBlockSize / 4;

 private:
  // Bit 0: mode
  //   0: jump distance.
  //   1: reference count.
  // Bits 1..31: either the reference count or the jump distance (the number
  // of consecutive unreferenced versions).
  // The initial value is 3 which means "the value is in reference count mode,
  // and the number of references is 1".
  std::atomic_uint32_t value_{3};
};

static_assert(sizeof(VersionRefCount) == 4);

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
