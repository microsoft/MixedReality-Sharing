// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <cstdint>

namespace Microsoft::MixedReality::Sharing::VersionedStorage {

// The handles described here are opaque enums that hide the actual nature of
// keys, payloads and subscriptions.
// The storage will be calling the methods of the provided Behavior object to
// operate with the handles (duplicate them, compare them etc).
// All handles imply the ownership.

enum class KeyHandle : uint64_t {};

enum class PayloadHandle : uint64_t {};

enum class KeySubscriptionHandle : uint64_t { kInvalid = 0 };

enum class SubkeySubscriptionHandle : uint64_t { kInvalid = 0 };

// Versions greater or equal to this value are considered to be invalid.
static constexpr uint64_t kInvalidVersion = 0x7FFF'FFFF'FFFF'FFFF;

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
