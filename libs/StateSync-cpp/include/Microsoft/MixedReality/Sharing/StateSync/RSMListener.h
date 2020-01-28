// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/StateSync/CommandId.h>

#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>

#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class RSMListener : public VirtualRefCountedBase {
 public:
  // Invoked when a new log entry is committed into the replicated log.
  // sequential_entry_id is exactly 1 greater than the sequential_entry_id of
  // the previous log entry.
  // command_id is the unique identifier of the command sent to the RSM.
  // The listener can use it to track the status of the commands
  // it sends to the RSM.
  virtual void OnEntryCommitted(uint64_t sequential_entry_id,
                                CommandId command_id,
                                std::string_view entry) noexcept = 0;

  // Placeholder interface.
  // There should be a handshake mechanism that allows the listener to catch up
  // without re-sending the entire state.
  virtual void OnLogFastForward(std::string_view state_blob) noexcept = 0;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
