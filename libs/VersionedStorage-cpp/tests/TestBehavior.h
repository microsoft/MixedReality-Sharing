// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <Microsoft/MixedReality/Sharing/VersionedStorage/Storage.h>

#include <atomic>
#include <mutex>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// Behavior object that creates simple integer-like keys and payloads and tracks
// the number of references to them.
class TestBehavior : public Behavior {
 public:
  void CheckLeakingHandles() noexcept;

  KeyHandle MakeKey(uint64_t id) noexcept;
  PayloadHandle MakePayload(uint64_t id) noexcept;

  uint32_t GetKeyReferenceCount(KeyHandle handle) const noexcept;
  uint32_t GetPayloadReferenceCount(PayloadHandle handle) const noexcept;

  uint64_t GetHash(KeyHandle handle) const noexcept override;
  bool Equal(KeyHandle a, KeyHandle b) const noexcept override;
  bool Less(KeyHandle a, KeyHandle b) const noexcept override;
  bool Equal(PayloadHandle a, PayloadHandle b) const noexcept override;

  void Release(KeyHandle handle) noexcept override;
  void Release(PayloadHandle handle) noexcept override;
  void Release(KeySubscriptionHandle handle) noexcept override;
  void Release(SubkeySubscriptionHandle handle) noexcept override;

  KeyHandle DuplicateHandle(KeyHandle handle) noexcept override;
  PayloadHandle DuplicateHandle(PayloadHandle handle) noexcept override;

  void* AllocateZeroedPages(size_t pages_count) noexcept override;
  void FreePages(void* address) noexcept override;

  void LockWriterMutex() noexcept override;
  void UnlockWriterMutex() noexcept override;

  uint64_t total_allocated_pages_count() const noexcept {
    return total_allocated_pages_count_.load(std::memory_order_relaxed);
  }

 private:
  std::mutex writer_mutex;

  struct KeyState {
    std::atomic_uint32_t reference_count_{0};
  };

  static constexpr size_t kKeysCount{32};
  KeyState key_states_[kKeysCount];

  struct PayloadState {
    std::atomic_uint32_t reference_count_{0};
  };

  static constexpr size_t kPayloadsCount{1024};
  PayloadState payload_states_[kPayloadsCount];

  static constexpr bool IsValid(KeyHandle handle) noexcept {
    return static_cast<size_t>(handle) < kKeysCount;
  }

  static constexpr bool IsValid(PayloadHandle handle) noexcept {
    return static_cast<size_t>(handle) < kPayloadsCount;
  }

  KeyState& GetKeyState(KeyHandle handle) noexcept;
  const KeyState& GetKeyState(KeyHandle handle) const noexcept;
  PayloadState& GetPayloadState(PayloadHandle handle) noexcept;
  const PayloadState& GetPayloadState(PayloadHandle handle) const noexcept;

  // Doesn't decrement when pages are freed.
  std::atomic_uint64_t total_allocated_pages_count_{0};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
