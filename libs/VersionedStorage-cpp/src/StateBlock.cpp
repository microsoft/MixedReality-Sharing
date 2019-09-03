// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include "src/StateBlock.h"

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

uint32_t KeyStateBlock::GetSubkeysCount(VersionOffset version_offset) const
    noexcept {
  // memory_order_acquire is required due to non-atomic loads in the loop below.
  // When the writer thread sets a new element for inplace_payloads_[i],
  // it then publishes the result by storing the new count with
  // release semantic.
  const uint32_t inplace_versions_count =
      inplace_versions_count_or_version_offset_.load(std::memory_order_acquire);
  // The latest available version is a lot likely to be requested, and there are
  // no more than 3 elements anyway, so the linear search is better here.
  for (uint32_t i = inplace_versions_count; i--;) {
    const VersionedSubkeysCount& inplace_payload = inplace_payloads_[i];
    if (inplace_payload.version_offset <= version_offset)
      return inplace_payload.subkeys_count;
  }
  return 0;
}

uint32_t KeyStateBlock::GetLatestSubkeysCount() const noexcept {
  const uint32_t inplace_versions_count =
      inplace_versions_count_or_version_offset_.load(std::memory_order_relaxed);
  return inplace_versions_count
             ? inplace_payloads_[inplace_versions_count - 1].subkeys_count
             : 0;
}

void KeyStateBlock::PushSubkeysCount(VersionOffset version_offset,
                                     uint32_t subkeys_count) noexcept {
  const uint32_t inplace_versions_count =
      inplace_versions_count_or_version_offset_.load(std::memory_order_relaxed);
  assert(inplace_versions_count < 3);
  inplace_payloads_[inplace_versions_count] = {version_offset, subkeys_count};
  inplace_versions_count_or_version_offset_.store(inplace_versions_count + 1,
                                                  std::memory_order_release);
}

VersionedPayloadHandle SubkeyStateBlock::GetVersionedPayload(
    uint64_t version) const noexcept {
  assert(version < kSmallestInvalidVersion);
  // We want to find the first payload with marked version that is less or equal
  // to the one we construct here. The search token has the last bit set,
  // so that a deletion marker of the same version can be found if it exists.
  const uint64_t search_token = (static_cast<uint64_t>(version) << 1) | 1;

  const uint64_t v0 = marked_version_0_.load(std::memory_order_acquire);
  if (v0 <= search_token) {
    const uint32_t offset = inplace_versions_count_or_version_offset_.load(
        std::memory_order_acquire);
    if (offset != static_cast<uint32_t>(VersionOffset::kInvalid)) {
      const uint64_t v1 = v0 + offset;
      if (v1 <= search_token) {
        return v1 & 1 ? VersionedPayloadHandle{}
                      : VersionedPayloadHandle{v1 >> 1, payloads_[1]};
      }
    }
    if ((v0 & 1) == 0)
      return {v0 >> 1, payloads_[0]};
  }
  return {};
}

VersionedPayloadHandle SubkeyStateBlock::GetLatestVersionedPayload() const
    noexcept {
  const uint64_t v0 = marked_version_0_.load(std::memory_order_relaxed);
  if (v0 < kInvalidMakredVersion) {
    const uint32_t offset = inplace_versions_count_or_version_offset_.load(
        std::memory_order_relaxed);
    if (offset != static_cast<uint32_t>(VersionOffset::kInvalid)) {
      const uint64_t v1 = v0 + offset;
      if ((v1 & 1) == 0)
        return VersionedPayloadHandle{v1 >> 1, payloads_[1]};
    } else if ((v0 & 1) == 0)
      return {v0 >> 1, payloads_[0]};
  }
  return {};
}

std::vector<VersionedPayloadHandle> SubkeyStateBlock::GetAllPayloads() const
    noexcept {
  // This method is called by the writer thread under the lock, so
  // memory_order_relaxed is enough.
  auto v0 = marked_version_0_.load(std::memory_order_relaxed);
  if (v0 != kInvalidMakredVersion) {
    std::vector<VersionedPayloadHandle> result;
    result.reserve(16);
    result.emplace_back(v0 >> 1, payloads_[0]);
    const uint32_t offset = inplace_versions_count_or_version_offset_.load(
        std::memory_order_relaxed);

    if (offset != static_cast<uint32_t>(VersionOffset::kInvalid)) {
      uint64_t v1 = v0 + offset;
      if ((v1 & 1) == 0) {
        result.emplace_back(v1 >> 1, payloads_[1]);
      }
    }
    return result;
  }
  return {};
}

void SubkeyStateBlock::Push(uint64_t version,
                            std::optional<PayloadHandle> payload) noexcept {
  assert(CanPush(version, payload.has_value()));
  const auto v0 = marked_version_0_.load(std::memory_order_relaxed);

  // The deletion marker bit (in case there is no payload) is set below.
  uint64_t marked_version = version << 1;

  if (v0 == kInvalidMakredVersion) {
    if (payload.has_value()) {
      payloads_[0] = *payload;
    } else {
      marked_version |= 1;
    }
    marked_version_0_.store(marked_version, std::memory_order_release);
  } else {
    uint32_t offset = static_cast<uint32_t>(marked_version - v0);
    if (payload.has_value()) {
      payloads_[1] = *payload;
    } else {
      offset += 1;
    }
    inplace_versions_count_or_version_offset_.store(offset,
                                                    std::memory_order_release);
  }
}

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
