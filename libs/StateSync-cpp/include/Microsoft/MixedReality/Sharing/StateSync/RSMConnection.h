// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/StateSync/CommandId.h>

#include <Microsoft/MixedReality/Sharing/Common/RefPtr.h>
#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>

#include <string>
#include <vector>

namespace Microsoft::MixedReality::Sharing::StateSync {

class RSMListener;
class NetworkManager;

class RSMConnection : public VirtualRefCountedBase {
 public:
  // Attempts to persist the command in the log of the RSM.
  virtual CommandId SendCommand(std::string_view command) = 0;

  // Processes a single incoming event of the RSM.
  // Returns true if there was an incoming event, false otherwise.
  virtual bool ProcessSingleUpdate(RSMListener& listener) = 0;

  // Creates a new replicated state machine using the provided name and
  // network_manager to react to remote attempts to connect to it.
  static RefPtr<RSMConnection> CreateSingleServerRSM(
      std::string name,
      RefPtr<NetworkManager> network_manager);

  // Connects to a remote single-server replicated state machine.
  static RefPtr<RSMConnection> ConnectToSingleServerRSM(
      std::string name,
      RefPtr<NetworkManager> network_manager,
      std::string server_connection_string);

  // Connects to a remote RAFT replicated state machine.
  // The servers decide what the current state is (if the leader is sure the
  // machine didn't exist, a new machine is created).
  //
  // This is a placeholder interface.
  static RefPtr<RSMConnection> ConnectToRaftRSM(
      std::string name,
      RefPtr<NetworkManager> network_manager,
      std::vector<std::string> servers);
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
