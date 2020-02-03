// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/InternedBlob.h>

#include <memory>
#include <string>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection {
 public:
  virtual ~NetworkConnection() noexcept {}

  virtual void SendMessage(std::string_view message) = 0;

  const RefPtr<const InternedBlob> connection_string;

 protected:
  NetworkConnection(RefPtr<const InternedBlob> connection_string)
      : connection_string{std::move(connection_string)} {}
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
