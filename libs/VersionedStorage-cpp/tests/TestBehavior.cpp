// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "pch.h"

#include "TestBehavior.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>
#include <Microsoft/MixedReality/Sharing/Common/hash.h>

#include <cassert>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

void TestBehavior::CheckLeakingHandles() noexcept {
  for (auto&& key_state : key_states_) {
    auto key_rc = key_state.reference_count_.load(std::memory_order_relaxed);
    EXPECT_EQ(key_rc, 0);
  }
  for (auto&& payload_state : payload_states_) {
    auto payload_rc =
        payload_state.reference_count_.load(std::memory_order_relaxed);
    EXPECT_EQ(payload_rc, 0);
  }
}

KeyHandle TestBehavior::MakeKey(size_t id) noexcept {
  assert(id < kKeysCount);
  KeyHandle handle{id};
  GetKeyState(handle).reference_count_.fetch_add(1, std::memory_order_relaxed);
  return handle;
}

PayloadHandle TestBehavior::MakePayload(size_t id) noexcept {
  assert(id < kPayloadsCount);
  PayloadHandle handle{id};
  GetPayloadState(handle).reference_count_.fetch_add(1,
                                                     std::memory_order_relaxed);
  return handle;
}

uint32_t TestBehavior::GetKeyReferenceCount(KeyHandle handle) const noexcept {
  return GetKeyState(handle).reference_count_.load(std::memory_order_relaxed);
}

uint32_t TestBehavior::GetPayloadReferenceCount(PayloadHandle handle) const
    noexcept {
  return GetPayloadState(handle).reference_count_.load(
      std::memory_order_relaxed);
}

uint64_t TestBehavior::GetHash(KeyHandle handle) const noexcept {
  return CalculateHash64(static_cast<uint64_t>(handle), 42);
}

bool TestBehavior::Equal(KeyHandle a, KeyHandle b) const noexcept {
  EXPECT_TRUE(IsValid(a) && IsValid(b));
  return a == b;
}

bool TestBehavior::Less(KeyHandle a, KeyHandle b) const noexcept {
  EXPECT_TRUE(IsValid(a) && IsValid(b));
  return a < b;
}

bool TestBehavior::Equal(PayloadHandle a, PayloadHandle b) const noexcept {
  EXPECT_TRUE(IsValid(a) && IsValid(b));
  return a == b;
}

void TestBehavior::Release(KeyHandle handle) noexcept {
  auto old_rc = GetKeyState(handle).reference_count_.fetch_sub(
      1, std::memory_order_relaxed);
  ASSERT_GT(old_rc, 0u);
}

void TestBehavior::Release(PayloadHandle handle) noexcept {
  auto old_rc = GetPayloadState(handle).reference_count_.fetch_sub(
      1, std::memory_order_relaxed);
  ASSERT_GT(old_rc, 0u);
}

void TestBehavior::Release(KeySubscriptionHandle handle) noexcept {
  // TODO: implement and test
}

void TestBehavior::Release(SubkeySubscriptionHandle handle) noexcept {
  // TODO: implement and test
}

KeyHandle TestBehavior::DuplicateHandle(KeyHandle handle) noexcept {
  auto old_rc = GetKeyState(handle).reference_count_.fetch_add(
      1, std::memory_order_relaxed);
  EXPECT_GT(old_rc, 0u);
  return handle;
}

PayloadHandle TestBehavior::DuplicateHandle(PayloadHandle handle) noexcept {
  auto old_rc = GetPayloadState(handle).reference_count_.fetch_add(
      1, std::memory_order_relaxed);
  EXPECT_GT(old_rc, 0u);
  return handle;
}

void* TestBehavior::AllocateZeroedPages(size_t pages_count) noexcept {
  total_allocated_pages_count_.fetch_add(pages_count,
                                         std::memory_order_relaxed);
  return Platform::AllocateZeroedPages(pages_count);
}

void TestBehavior::FreePages(void* address) noexcept {
  Platform::FreePages(address);
}

void TestBehavior::LockWriterMutex() noexcept {
  writer_mutex.lock();
}
void TestBehavior::UnlockWriterMutex() noexcept {
  writer_mutex.unlock();
}

TestBehavior::KeyState& TestBehavior::GetKeyState(KeyHandle handle) noexcept {
  EXPECT_TRUE(IsValid(handle));
  return key_states_[static_cast<size_t>(handle)];
}

const TestBehavior::KeyState& TestBehavior::GetKeyState(KeyHandle handle) const
    noexcept {
  EXPECT_TRUE(IsValid(handle));
  return key_states_[static_cast<size_t>(handle)];
}

TestBehavior::PayloadState& TestBehavior::GetPayloadState(
    PayloadHandle handle) noexcept {
  EXPECT_TRUE(IsValid(handle));
  return payload_states_[static_cast<size_t>(handle)];
}

const TestBehavior::PayloadState& TestBehavior::GetPayloadState(
    PayloadHandle handle) const noexcept {
  EXPECT_TRUE(IsValid(handle));
  return payload_states_[static_cast<size_t>(handle)];
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
