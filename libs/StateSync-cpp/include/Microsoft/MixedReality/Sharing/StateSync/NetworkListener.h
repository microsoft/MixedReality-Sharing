// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>

#include <memory>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection;

class NetworkListener : public VirtualRefCountedBase {
 public:
  virtual void OnMessage(NetworkConnection& sender, std::string_view data) = 0;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
