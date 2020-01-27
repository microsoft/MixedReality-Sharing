// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <memory>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection;

class NetworkListener {
 public:
  virtual ~NetworkListener() noexcept {}

  virtual void OnMessage(const NetworkConnection& sender,
                         std::string_view data) = 0;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
