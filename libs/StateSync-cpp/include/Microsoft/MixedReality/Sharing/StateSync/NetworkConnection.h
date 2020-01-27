// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <memory>
#include <string>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection
    : public std::enable_shared_from_this<NetworkConnection> {
 public:
  virtual ~NetworkConnection() noexcept {}

  virtual void SendMessage(std::string_view data) noexcept = 0;

  const std::string connection_string;

 protected:
  NetworkConnection(std::string connection_string)
      : connection_string{std::move(connection_string)} {}

  NetworkConnection(const NetworkConnection&) = delete;
  NetworkConnection& operator=(const NetworkConnection&) = delete;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
