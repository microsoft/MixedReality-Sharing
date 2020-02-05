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
  uint64_t data_[2];

  // Generates a random command id that is likely to be globally unique,
  // but shouldn't be used in a cryptographic context.
  static CommandId GenerateRandom() noexcept;

  CommandId& operator++() noexcept {
    if (++data_[0] == 0)
      ++data_[1];
    return *this;
  }
};

inline bool operator==(const CommandId& a, const CommandId& b) noexcept {
  return a.data_[0] == b.data_[0] && a.data_[1] == b.data_[1];
}

inline bool operator!=(const CommandId& a, const CommandId& b) noexcept {
  return a.data_[0] != b.data_[0] || a.data_[1] != b.data_[1];
}

}  // namespace Microsoft::MixedReality::Sharing::StateSync
