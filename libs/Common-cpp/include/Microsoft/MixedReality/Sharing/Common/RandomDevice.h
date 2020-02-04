// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// The content of this file is based on xoshiro256++ by David Blackman and
// Sebastiano Vigna (vigna@acm.org), which is released into the public domain:
// http://prng.di.unimi.it/

#pragma once

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

#include <cstdint>

namespace Microsoft::MixedReality::Sharing {

// Simple non-cryptographic xoshiro256++ pseudo-random number generator.
// The interface is compatible with standard random engines, and thus the
// generator can be used with distributions from <random>.
// See thread_instance() for the most common intended use case.
class alignas(64) RandomDevice {
 public:
  enum class InitializeFromGlobalState {};
  using result_type = uint64_t;
  static constexpr result_type min() noexcept { return 0; }
  static constexpr result_type max() noexcept { return ~0ull; }

  // Returns a reference to the thread-local instance of the random device.
  // Each thread receives its own state that is separated from the state of any
  // other thread by at least 2^128 calls to operator().
  //
  // Do not expose this state to any other threads (operator() is expected to be
  // called from the same thread that called Get()).
  //
  // Expected usage:
  //   auto& rng = RandomDevice::thread_instance();
  //   uint64_t random_64_bits = rng();
  //   std::uniform_int_distribution<int> dist(1, 10);
  //   auto random_from_1_to_10 = dist(rng);
  //   ...
  static RandomDevice& thread_instance() {
    thread_local RandomDevice state{InitializeFromGlobalState{}};
    return state;
  }

  // Advances the internal state and returns a uniformly distributed 64-bit
  // pseudo-random number.
  uint64_t operator()() noexcept;

  // Constructor for the thread-local instance,
  // which calls Jump on the global state and copies it.
  // The enum tag is fictive and should prevent accidental misuse (such as
  // constructing a new state instead of obtaining a thread instance).
  RandomDevice(InitializeFromGlobalState) noexcept;

  // Constructor for testing the behavior of the random device.
  RandomDevice(uint64_t s0, uint64_t s1, uint64_t s2, uint64_t s3) noexcept;

  // Do not use on the thread-local instance, or the random numbers of different
  // threads will start to collide. See Jump() for details.
  void JumpForTestingPurposesOnly() noexcept { Jump(); }

 private:
  RandomDevice(const RandomDevice&) = delete;
  RandomDevice& operator=(const RandomDevice&) = delete;

  // Constructor for the global state.
  RandomDevice() noexcept;

  // Quickly advances the state by 2^128 calls to operator().
  void Jump() noexcept;

  static uint64_t RotateLeft(const uint64_t x, int k) noexcept;

  uint64_t state_[4];
};

MS_MR_SHARING_FORCEINLINE
uint64_t RandomDevice::RotateLeft(const uint64_t x, int k) noexcept {
  return (x << k) | (x >> (64 - k));
}

MS_MR_SHARING_FORCEINLINE
uint64_t RandomDevice::operator()() noexcept {
  const uint64_t result = RotateLeft(state_[0] + state_[3], 23) + state_[0];
  const uint64_t t = state_[1] << 17;
  state_[2] ^= state_[0];
  state_[3] ^= state_[1];
  state_[1] ^= state_[2];
  state_[0] ^= state_[3];
  state_[2] ^= t;
  state_[3] = RotateLeft(state_[3], 45);
  return result;
}

}  // namespace Microsoft::MixedReality::Sharing
