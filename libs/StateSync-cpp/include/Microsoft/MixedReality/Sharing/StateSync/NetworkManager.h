// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/InternedBlob.h>
#include <Microsoft/MixedReality/Sharing/Common/RefPtr.h>
#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>

#include <memory>
#include <string>

namespace Microsoft::MixedReality::Sharing::StateSync {

class NetworkConnection;
class NetworkListener;

class NetworkManager : public VirtualRefCountedBase {
 public:
  // Returns a connection object that can be used to send messages to the remote
  // endpoint described by connection_string
  virtual std::shared_ptr<NetworkConnection> GetConnection(
      const InternedBlob& connection_string) = 0;

  // Placeholder interface: add ways that do not require manual polling.
  virtual bool PollMessage(NetworkListener& listener) = 0;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
