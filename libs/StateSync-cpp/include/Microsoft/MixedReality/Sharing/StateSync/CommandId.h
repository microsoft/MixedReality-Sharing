// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <cstdint>

namespace Microsoft::MixedReality::Sharing::StateSync {

// The unique identifier of a command.
// When the user of the replicated state machine sends a command
// a unique CommandId is generated, which can later be used to track
// the status of the command and ensure that the command is appended
// at most once.
struct CommandId {
  uint64_t data[2];
};

inline bool operator==(const CommandId& a, const CommandId& b) noexcept {
  return a.data[0] == b.data[0] && a.data[1] == b.data[1];
}

inline bool operator!=(const CommandId& a, const CommandId& b) noexcept {
  return a.data[0] != b.data[0] || a.data[1] != b.data[1];
}

}  // namespace Microsoft::MixedReality::Sharing::StateSync
