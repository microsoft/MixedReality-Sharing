// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/Key.h>

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>
#include <Microsoft/MixedReality/Sharing/Common/hash.h>

#include <algorithm>
#include <cstddef>
#include <cstring>
#include <memory>
#include <mutex>
#include <unordered_set>

namespace Microsoft::MixedReality::Sharing::StateSync {
namespace {
// FIXME: this is a temporary slow implementation of the interning mechanism.
struct KeyShard {
  struct KeyPtrHash {
    MS_MR_SHARING_FORCEINLINE
    size_t operator()(const Key* key) const noexcept {
      return static_cast<size_t>(key->hash());
    }
  };

  struct KeyPtrEqual {
    MS_MR_SHARING_FORCEINLINE
    size_t operator()(const Key* a, const Key* b) const noexcept {
      return a == b || a->size() == b->size() && a->hash() == b->hash() &&
                           0 == memcmp(a->data(), b->data(), a->size());
    }
  };

  std::unordered_set<const Key*, KeyPtrHash, KeyPtrEqual> keys_;
  std::mutex mutex_;
};

KeyShard& GetKeyShard(uint64_t hash) {
  static constexpr size_t kShardsCountLog = 6;
  static KeyShard shards[1u << kShardsCountLog];
  return shards[hash >> (64 - kShardsCountLog)];
}
}  // namespace

inline Key::Key(uint64_t hash, const char* data, size_t size) noexcept
    : hash_{hash}, size_{size} {
  if (size) {
    memcpy(data_, data, size);
  }
}

void Key::RemoveRef() const noexcept {
  // We should read the hash before doing the atomic operation, because some
  // other thread may destroy this object ahead of us, see the details below.
  const uint64_t hash = hash_;
  if (ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
    KeyShard& shard = GetKeyShard(hash);
    {
      auto lock = std::lock_guard(shard.mutex_);
      auto it = shard.keys_.find(this);

      // Threads that are performing the interning are allowed to resurrect the
      // keys with no references. What can happen is:
      // * This thread drops the ref_count_ to 0
      // * Some other thread resurrects the key
      // * Then it removes the last reference, cleans up the map and destroys
      //   the key.
      // As a result, we won't find the key in the map, and this would be
      // invalid. We don't need to perform any actions in this case.
      //
      // Once the ref_count_ drops to 0, it can only increase under this
      // lock. Since we are holding this lock, we can be sure that this
      // object is still alive, has no alive references, won't get any new
      // references, and therefore should be deleted.
      if (it == shard.keys_.end() ||
          ref_count_.load(std::memory_order_relaxed) != 0)
        return;
      // Erasing should be performed under the lock, but destroying the key can
      // be done outside.
      shard.keys_.erase(it);
    }
    this->~Key();
    free(const_cast<Key*>(this));
  }
}

RefPtr<Key> Key::Create(std::string_view content) noexcept {
  const uint64_t hash = CalculateHash64(content);

  // FIXME: this is a temporary slow implementation of the interning mechanism.
  // We shouldn't pessimistically allocate anything, and for already
  // existing keys the interning can be made lock-free.

  const size_t alloc_size =
      std::max(sizeof(Key), offsetof(Key, data_) + content.size());
  void* raw_ptr = malloc(alloc_size);
  Key* candidate = new (raw_ptr) Key{hash, content.data(), content.size()};
  const Key* existing = nullptr;
  KeyShard& shard = GetKeyShard(hash);
  {
    auto lock = std::lock_guard(shard.mutex_);
    auto&& [it, was_inserted] = shard.keys_.emplace(candidate);
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
  candidate->~Key();
  free(candidate);
  // The reference was already added under the lock above.
  return {const_cast<Key*>(existing), DontAddRef{}};
}

bool Key::OrderedLess(const Key& other) const noexcept {
  if (this == &other)
    return false;

  // Sizes are compared first.
  // This ordering is likely to be faster than lexicographical.
  return size_ == other.size_ ? memcmp(data_, other.data_, size_) < 0
                              : size_ < other.size_;
}

bool Key::OrderedLess(std::string_view sv) const noexcept {
  // Sizes are compared first.
  // This ordering is likely to be faster than lexicographical.
  return size_ == sv.size() ? memcmp(data_, sv.data(), size_) < 0
                            : size_ < sv.size();
}

bool Key::OrderedGreater(std::string_view sv) const noexcept {
  // Sizes are compared first.
  // This ordering is likely to be faster than lexicographical.
  return size_ == sv.size() ? memcmp(data_, sv.data(), size_) > 0
                            : size_ > sv.size();
}

}  // namespace Microsoft::MixedReality::Sharing::StateSync
