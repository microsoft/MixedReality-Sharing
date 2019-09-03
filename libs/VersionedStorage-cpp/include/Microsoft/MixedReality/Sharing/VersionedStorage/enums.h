// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

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

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage
