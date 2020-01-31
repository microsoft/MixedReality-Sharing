// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/InternedBlob.h>

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>
#include <Microsoft/MixedReality/Sharing/Common/hash.h>

#include <algorithm>
#include <cstddef>
#include <cstring>
#include <memory>
#include <mutex>
#include <unordered_set>

namespace Microsoft::MixedReality::Sharing {
namespace {
// FIXME: this is a temporary slow implementation of the interning mechanism.
struct BlobShard {
  struct BlobPtrHash {
    MS_MR_SHARING_FORCEINLINE
    size_t operator()(const InternedBlob* blob) const noexcept {
      return static_cast<size_t>(blob->hash());
    }
  };

  struct BlobPtrEqual {
    MS_MR_SHARING_FORCEINLINE
    size_t operator()(const InternedBlob* a, const InternedBlob* b) const
        noexcept {
      return (a == b) ||
             ((a->size() == b->size()) && (a->hash() == b->hash()) &&
              (0 == memcmp(a->data(), b->data(), a->size())));
    }
  };

  std::unordered_set<const InternedBlob*, BlobPtrHash, BlobPtrEqual> blobs_;
  std::mutex mutex_;
};

BlobShard& GetBlobShard(uint64_t hash) {
  static constexpr size_t kShardsCountLog = 6;
  static BlobShard shards[1u << kShardsCountLog];
  return shards[hash >> (64 - kShardsCountLog)];
}
}  // namespace

inline InternedBlob::InternedBlob(uint64_t hash,
                                  const char* data,
                                  int size) noexcept
    : size_{size}, hash_{hash} {
  memcpy(data_, data, size);
}

void InternedBlob::RemoveRef() const noexcept {
  // We should read the hash before doing the atomic operation, because some
  // other thread may destroy this object ahead of us, see the details below.
  const uint64_t hash = hash_;
  if (ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
    BlobShard& shard = GetBlobShard(hash);
    {
      auto lock = std::lock_guard(shard.mutex_);
      auto it = shard.blobs_.find(this);

      // Threads that are performing the interning are allowed to resurrect the
      // blobs with no references. What can happen is:
      // * This thread drops the ref_count_ to 0
      // * Some other thread resurrects the blob
      // * Then it removes the last reference, cleans up the map and destroys
      //   the blob.
      // As a result, we won't find the blob in the map, and this would be
      // invalid. We don't need to perform any actions in this case.
      //
      // Once the ref_count_ drops to 0, it can only increase under this
      // lock. Since we are holding this lock, we can be sure that this
      // object is still alive, has no alive references, won't get any new
      // references, and therefore should be deleted.
      if (it == shard.blobs_.end() ||
          ref_count_.load(std::memory_order_relaxed) != 0)
        return;
      // Erasing should be performed under the lock, but destroying the blob can
      // be done outside.
      shard.blobs_.erase(it);
    }
    this->~InternedBlob();
    free(const_cast<InternedBlob*>(this));
  }
}

RefPtr<const InternedBlob> InternedBlob::Create(const char* data, size_t size) {
  if (size > kMaxSize) {
    throw std::invalid_argument(
        "Can't create InternedBlob: the blob can't be larger than 2147483647 "
        "bytes.");
  }
  const uint64_t hash = CalculateHash64(data, size);

  // FIXME: this is a temporary slow implementation of the interning mechanism.
  // We shouldn't pessimistically allocate anything, and for already
  // existing blobs the interning can be made lock-free.

  const size_t alloc_size =
      std::max(sizeof(InternedBlob), offsetof(InternedBlob, data_) + size);
  void* raw_ptr = malloc(alloc_size);
  InternedBlob* candidate =
      new (raw_ptr) InternedBlob{hash, data, static_cast<int>(size)};
  const InternedBlob* existing = nullptr;
  BlobShard& shard = GetBlobShard(hash);
  {
    auto lock = std::lock_guard(shard.mutex_);
    auto&& [it, was_inserted] = shard.blobs_.emplace(candidate);
    if (was_inserted) {
      // ref_count_ of the candidate starts with 1
      return {candidate, DontAddRef{}};
    }
    existing = *it;
    // We don't care if the refcount was 0 (because the last reference just
    // expired). RemoveRef that did that will perform the cleanup under the
    // lock, and will check the refcount there again. So there should be no
    // race with the cleanup code as long as we add the reference under the
    // same lock.
    existing->AddRef();
  }
  candidate->~InternedBlob();
  free(candidate);
  // The reference was already added under the lock above.
  return {const_cast<InternedBlob*>(existing), DontAddRef{}};
}

bool InternedBlob::OrderedLess(const InternedBlob& other) const noexcept {
  if (this == &other)
    return false;

  // Sizes are compared first.
  // This ordering is likely to be faster than lexicographical.
  return size_ == other.size_ ? memcmp(data_, other.data_, size_) < 0
                              : size_ < other.size_;
}

bool InternedBlob::OrderedLess(std::string_view sv) const noexcept {
  // Sizes are compared first.
  // This ordering is likely to be faster than lexicographical.
  return size_ == sv.size() ? memcmp(data_, sv.data(), size_) < 0
                            : size_ < sv.size();
}

bool InternedBlob::OrderedGreater(std::string_view sv) const noexcept {
  // Sizes are compared first.
  // This ordering is likely to be faster than lexicographical.
  return size_ == sv.size() ? memcmp(data_, sv.data(), size_) > 0
                            : size_ > sv.size();
}

}  // namespace Microsoft::MixedReality::Sharing
