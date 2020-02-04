// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/ReplicatedState.h>

namespace Microsoft::MixedReality::Sharing::StateSync {

ReplicatedState::ReplicatedState(Guid guid, RefPtr<RSMConnection> connection)
    : guid_{guid}, connection_{std::move(connection)} {}

ReplicatedState::~ReplicatedState() noexcept = default;

RefPtr<ReplicatedState> ReplicatedState::Create(
    Guid guid,
    RefPtr<RSMConnection> connection) noexcept {
  return new ReplicatedState{guid, std::move(connection)};
}

}  // namespace Microsoft::MixedReality::Sharing::StateSync
