// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>

#include <memory>
#include <string>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection : public VirtualRefCountedBase {
 public:
  virtual void SendMessage(std::string_view data) noexcept = 0;

  const std::string connection_string;

 protected:
  NetworkConnection(std::string connection_string)
      : connection_string{std::move(connection_string)} {}
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
