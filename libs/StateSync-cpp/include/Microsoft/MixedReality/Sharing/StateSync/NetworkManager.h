// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <memory>
#include <string>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection;
class NetworkListener;

class NetworkManager {
 public:
  virtual ~NetworkManager() noexcept {}

  // Returns a connection object that can be used to send messages to the remote
  // endpoint described by connection_string
  virtual std::shared_ptr<NetworkConnection> GetConnection(
      std::string connection_string) = 0;

  // Placeholder interface: add ways that do not require manual polling.
  virtual bool PollMessage(NetworkListener& listener) = 0;

 private:
  NetworkManager(const NetworkManager&) = delete;
  NetworkManager& operator=(const NetworkManager&) = delete;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
