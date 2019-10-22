// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

KeyHandle TestBehavior::MakeKey(uint64_t id) noexcept {
  assert(id < kKeysCount);
  KeyHandle handle{id};
  GetKeyState(handle).reference_count_.fetch_add(1, std::memory_order_relaxed);
  return handle;
}

PayloadHandle TestBehavior::MakePayload(uint64_t id) noexcept {
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

uint64_t TestBehavior::GetKeyHash(KeyHandle handle) const noexcept {
  return CalculateHash64(static_cast<uint64_t>(handle), 42);
}

uint64_t TestBehavior::GetKeyHash(std::string_view serialized_key_handle) const
    noexcept {
  // For simplicity, the test just assumes that the key is always deserializable
  // here.
  uint64_t handle;
  assert(serialized_key_handle.size() == 8);
  memcpy(&handle, serialized_key_handle.data(), 8);
  return CalculateHash64(handle, 42);
}

bool TestBehavior::Equal(KeyHandle a, KeyHandle b) const noexcept {
  EXPECT_TRUE(IsValid(a) && IsValid(b));
  return a == b;
}

bool TestBehavior::Equal(KeyHandle key_handle,
                         std::string_view serialized_payload) const noexcept {
  constexpr auto size = sizeof(key_handle);
  return serialized_payload.size() == size &&
         0 == memcmp(&key_handle, serialized_payload.data(), size);
}

bool TestBehavior::Equal(PayloadHandle payload_handle,
                         std::string_view serialized_payload) const noexcept {
  constexpr auto size = sizeof(payload_handle);
  return serialized_payload.size() == size &&
         0 == memcmp(&payload_handle, serialized_payload.data(), size);
}

bool TestBehavior::Less(KeyHandle a, KeyHandle b) const noexcept {
  EXPECT_TRUE(IsValid(a) && IsValid(b));
  return a < b;
}

bool TestBehavior::Less(std::string_view a, KeyHandle b) const noexcept {
  // Invalid inputs are considered to be less.
  return a.size() != sizeof(b) || memcmp(a.data(), &b, sizeof(b)) < 0;
}

bool TestBehavior::Less(KeyHandle a, std::string_view b) const noexcept {
  // Invalid inputs are considered to be less
  // (so 'b' must be valid for 'a' to be less).
  return b.size() == sizeof(a) && memcmp(&a, b.data(), sizeof(a)) < 0;
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

size_t TestBehavior::Serialize(KeyHandle handle,
                               std::vector<std::byte>& byte_stream) {
  size_t size = byte_stream.size();
  byte_stream.resize(size + sizeof(handle));
  memcpy(byte_stream.data() + size, &handle, sizeof(handle));
  return sizeof(handle);
}

size_t TestBehavior::Serialize(PayloadHandle handle,
                               std::vector<std::byte>& byte_stream) {
  size_t size = byte_stream.size();
  byte_stream.resize(size + sizeof(handle));
  memcpy(byte_stream.data() + size, &handle, sizeof(handle));
  return sizeof(handle);
}

KeyHandle TestBehavior::DeserializeKey(std::string_view serialized_payload) {
  if (serialized_payload.size() != sizeof(KeyHandle))
    throw std::invalid_argument{
        "Can't deserialize a key handle: expected 8 bytes of input"};

  KeyHandle result;
  memcpy(&result, serialized_payload.data(), sizeof(KeyHandle));
  GetKeyState(result).reference_count_.fetch_add(1, std::memory_order_relaxed);
  return result;
}

PayloadHandle TestBehavior::DeserializePayload(
    std::string_view serialized_payload) {
  if (serialized_payload.size() != sizeof(PayloadHandle))
    throw std::invalid_argument{
        "Can't deserialize a payload handle: expected 8 bytes of input"};

  PayloadHandle result;
  memcpy(&result, serialized_payload.data(), sizeof(PayloadHandle));
  GetPayloadState(result).reference_count_.fetch_add(1,
                                                     std::memory_order_relaxed);
  return result;
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
