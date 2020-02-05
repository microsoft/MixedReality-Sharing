// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/CommandId.h>

#include <Microsoft/MixedReality/Sharing/Common/RandomDevice.h>

namespace Microsoft::MixedReality::Sharing::StateSync {

CommandId CommandId::GenerateRandom() noexcept {
  auto& rng = RandomDevice::thread_instance();
  return {rng(), rng()};
}

}  // namespace Microsoft::MixedReality::Sharing::StateSync
