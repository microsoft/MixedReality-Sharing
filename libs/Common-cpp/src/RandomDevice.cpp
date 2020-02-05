// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// The content of this file is based on xoshiro256++ by David Blackman and
// Sebastiano Vigna (vigna@acm.org), which is released into the public domain:
// http://prng.di.unimi.it/

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/RandomDevice.h>

#include <cstdlib>
#include <mutex>
#include <random>

namespace Microsoft::MixedReality::Sharing {

RandomDevice::RandomDevice() noexcept {
  // Assuming here that random_device is good enough for generating the global
  // random state.
  // If its quality is low, the thread-local generators should be good enough
  // anyway, but there is a much higher risk of global collisions between the
  // generators on different devices (if the distribution produces the same
  // initializing sequence on different devices).
  std::random_device rd{};

  std::uniform_int_distribution<uint64_t> dist{0, ~0ull};
  static constexpr int kRetriesCount = 1024;
  for (int i = 0; i < kRetriesCount; ++i) {
    for (auto& s : state_)
      s = dist(rd);
    // Retrying until something is non-0
    if (state_[0] != 0 || state_[1] != 0 || state_[2] != 0 || state_[3] == 0)
      return;
  }
  assert(!"Unable to generate a random non-0 state for the random number generator");
  abort();  // This is not actionable by the user.
}

RandomDevice::RandomDevice(InitializeFromGlobalState) noexcept {
  // Each thread-local instance advances the global state by 2^128 stages,
  // and takes a copy of it.
  // The global state is not used for any purpose other than this.

  static RandomDevice global_state;
  static std::mutex global_state_mutex;
  auto lock = std::lock_guard{global_state_mutex};
  global_state.Jump();
  for (size_t i = 0; i < 4; ++i)
    state_[i] = global_state.state_[i];
}

RandomDevice::RandomDevice(uint64_t s0,
                           uint64_t s1,
                           uint64_t s2,
                           uint64_t s3) noexcept {
  state_[0] = s0;
  state_[1] = s1;
  state_[2] = s2;
  state_[3] = s3;
}

void RandomDevice::Jump() noexcept {
  // The jump constants are obtained from the reference implementation.
  static constexpr uint64_t kJumpConstants[] = {
      0x180ec6d33cfd0aba, 0xd5a61266f0c9392c, 0xa9582618e03fc9aa,
      0x39abdc4529b1661c};
  uint64_t s0 = 0;
  uint64_t s1 = 0;
  uint64_t s2 = 0;
  uint64_t s3 = 0;
  for (auto& constant : kJumpConstants) {
    for (int b = 0; b < 64; b++) {
      if (constant & (1ull << b)) {
        s0 ^= state_[0];
        s1 ^= state_[1];
        s2 ^= state_[2];
        s3 ^= state_[3];
      }
      (*this)();
    }
  }
  state_[0] = s0;
  state_[1] = s1;
  state_[2] = s2;
  state_[3] = s3;
}

}  // namespace Microsoft::MixedReality::Sharing
