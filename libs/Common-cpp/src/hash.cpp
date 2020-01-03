// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// The content of this file is based on wyhash by Wang Yi, which is released
// into the public domain: https://github.com/wangyi-fudan/wyhash

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>
#include <Microsoft/MixedReality/Sharing/Common/hash.h>

#if defined(_MSC_VER) && defined(_M_X64)
#include <intrin.h>
#pragma intrinsic(_umul128)
#endif

// Using the 32-bit-friendly variation to make the performance consistent
// on all platforms.
// It's slower than the default one on x64/ARM64, but much faster on x86.
#define WYHASH32

namespace Microsoft::MixedReality::Sharing {
namespace {

constexpr uint64_t _wyp0 = 0xa0761d6478bd642full;
constexpr uint64_t _wyp1 = 0xe7037ed1a0b428dbull;
constexpr uint64_t _wyp2 = 0x8ebc6af09c88c6e3ull;
constexpr uint64_t _wyp3 = 0x589965cc75374cc3ull;
constexpr uint64_t _wyp4 = 0x1d8e4e27c47d124full;

MS_MR_SHARING_FORCEINLINE uint64_t _wyrotr(uint64_t v, unsigned k) {
  return (v >> k) | (v << (64 - k));
}

MS_MR_SHARING_FORCEINLINE uint64_t _wymum(uint64_t A, uint64_t B) {
#ifdef WYHASH32
  uint64_t hh = (A >> 32) * (B >> 32);
  uint64_t hl = (A >> 32) * (unsigned)B;
  uint64_t lh = (unsigned)A * (B >> 32);
  uint64_t ll = (uint64_t)(unsigned)A * (unsigned)B;
  return _wyrotr(hl, 32) ^ _wyrotr(lh, 32) ^ hh ^ ll;
#else
#ifdef __SIZEOF_INT128__
  __uint128_t r = A;
  r *= B;
  return (r >> 64) ^ r;
#elif defined(_MSC_VER) && defined(_M_X64)
  A = _umul128(A, B, &B);
  return A ^ B;
#else
  uint64_t ha = A >> 32;
  uint64_t hb = B >> 32;
  uint64_t la = (uint32_t)A;
  uint64_t lb = (uint32_t)B;
  uint64_t rh = ha * hb;
  uint64_t rm0 = ha * lb;
  uint64_t rm1 = hb * la;
  uint64_t rl = la * lb;
  uint64_t t = rl + (rm0 << 32);
  uint64_t c = t < rl;
  uint64_t lo = t + (rm1 << 32);
  c += lo < t;
  uint64_t hi = rh + (rm0 >> 32) + (rm1 >> 32) + c;
  return hi ^ lo;
#endif
#endif
}

MS_MR_SHARING_FORCEINLINE uint64_t _wyr8(const uint8_t* p) {
  uint64_t v;
  memcpy(&v, p, 8);
  return v;
}

MS_MR_SHARING_FORCEINLINE uint64_t _wyr4(const uint8_t* p) {
  uint32_t v;
  memcpy(&v, p, 4);
  return v;
}

MS_MR_SHARING_FORCEINLINE uint64_t _wyr3(const uint8_t* p, unsigned k) {
  return (((uint64_t)p[0]) << 16) | (((uint64_t)p[k >> 1]) << 8) | p[k - 1];
}

}  // namespace

uint64_t CalculateHash64(const char* data,
                         size_t size,
                         uint64_t seed) noexcept {
  if (MS_MR_UNLIKELY(!size))
    return 0;

  uint64_t len = size;

  const uint8_t* p = reinterpret_cast<const uint8_t*>(data);
  if (len < 4)
    return _wymum(_wymum(_wyr3(p, static_cast<unsigned>(size)) ^ seed ^ _wyp0,
                         seed ^ _wyp1),
                  len ^ _wyp4);
  else if (len <= 8)
    return _wymum(
        _wymum(_wyr4(p) ^ seed ^ _wyp0, _wyr4(p + len - 4) ^ seed ^ _wyp1),
        len ^ _wyp4);
  else if (len <= 16)
    return _wymum(
        _wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + len - 8) ^ seed ^ _wyp1),
        len ^ _wyp4);
  else if (len <= 24)
    return _wymum(_wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + 8) ^ seed ^ _wyp1) ^
                      _wymum(_wyr8(p + len - 8) ^ seed ^ _wyp2, seed ^ _wyp3),
                  len ^ _wyp4);
  else if (len <= 32)
    return _wymum(_wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + 8) ^ seed ^ _wyp1) ^
                      _wymum(_wyr8(p + 16) ^ seed ^ _wyp2,
                             _wyr8(p + len - 8) ^ seed ^ _wyp3),
                  len ^ _wyp4);
  uint64_t see1 = seed;
  uint64_t i = len;
  if (i >= 256)
    for (; i >= 256; i -= 256, p += 256) {
      seed = _wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + 8) ^ seed ^ _wyp1) ^
             _wymum(_wyr8(p + 16) ^ seed ^ _wyp2, _wyr8(p + 24) ^ seed ^ _wyp3);
      see1 =
          _wymum(_wyr8(p + 32) ^ see1 ^ _wyp1, _wyr8(p + 40) ^ see1 ^ _wyp2) ^
          _wymum(_wyr8(p + 48) ^ see1 ^ _wyp3, _wyr8(p + 56) ^ see1 ^ _wyp0);
      seed =
          _wymum(_wyr8(p + 64) ^ seed ^ _wyp0, _wyr8(p + 72) ^ seed ^ _wyp1) ^
          _wymum(_wyr8(p + 80) ^ seed ^ _wyp2, _wyr8(p + 88) ^ seed ^ _wyp3);
      see1 =
          _wymum(_wyr8(p + 96) ^ see1 ^ _wyp1, _wyr8(p + 104) ^ see1 ^ _wyp2) ^
          _wymum(_wyr8(p + 112) ^ see1 ^ _wyp3, _wyr8(p + 120) ^ see1 ^ _wyp0);
      seed =
          _wymum(_wyr8(p + 128) ^ seed ^ _wyp0, _wyr8(p + 136) ^ seed ^ _wyp1) ^
          _wymum(_wyr8(p + 144) ^ seed ^ _wyp2, _wyr8(p + 152) ^ seed ^ _wyp3);
      see1 =
          _wymum(_wyr8(p + 160) ^ see1 ^ _wyp1, _wyr8(p + 168) ^ see1 ^ _wyp2) ^
          _wymum(_wyr8(p + 176) ^ see1 ^ _wyp3, _wyr8(p + 184) ^ see1 ^ _wyp0);
      seed =
          _wymum(_wyr8(p + 192) ^ seed ^ _wyp0, _wyr8(p + 200) ^ seed ^ _wyp1) ^
          _wymum(_wyr8(p + 208) ^ seed ^ _wyp2, _wyr8(p + 216) ^ seed ^ _wyp3);
      see1 =
          _wymum(_wyr8(p + 224) ^ see1 ^ _wyp1, _wyr8(p + 232) ^ see1 ^ _wyp2) ^
          _wymum(_wyr8(p + 240) ^ see1 ^ _wyp3, _wyr8(p + 248) ^ see1 ^ _wyp0);
    }
  for (; i >= 32; i -= 32, p += 32) {
    seed = _wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + 8) ^ seed ^ _wyp1);
    see1 = _wymum(_wyr8(p + 16) ^ see1 ^ _wyp2, _wyr8(p + 24) ^ see1 ^ _wyp3);
  }
  if (!i) {
  } else if (i < 4)
    seed =
        _wymum(_wyr3(p, static_cast<unsigned>(i)) ^ seed ^ _wyp0, seed ^ _wyp1);
  else if (i <= 8)
    seed = _wymum(_wyr4(p) ^ seed ^ _wyp0, _wyr4(p + i - 4) ^ seed ^ _wyp1);
  else if (i <= 16)
    seed = _wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + i - 8) ^ seed ^ _wyp1);
  else if (i <= 24) {
    seed = _wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + 8) ^ seed ^ _wyp1);
    see1 = _wymum(_wyr8(p + i - 8) ^ see1 ^ _wyp2, see1 ^ _wyp3);
  } else {
    seed = _wymum(_wyr8(p) ^ seed ^ _wyp0, _wyr8(p + 8) ^ seed ^ _wyp1);
    see1 =
        _wymum(_wyr8(p + 16) ^ see1 ^ _wyp2, _wyr8(p + i - 8) ^ see1 ^ _wyp3);
  }
  return _wymum(seed ^ see1, len ^ _wyp4);
}

uint64_t CalculateHash64(uint64_t value_a, uint64_t value_b) noexcept {
  return _wymum(_wymum(value_a ^ _wyp0, value_b ^ _wyp1), _wyp2);
}

}  // namespace Microsoft::MixedReality::Sharing
