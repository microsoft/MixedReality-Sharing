// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

// The content of this file is based on wyhash by Wang Yi, which is released
// into the public domain: https://github.com/wangyi-fudan/wyhash

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/hash.h>

#if defined(_MSC_VER) && defined(_M_X64)
#include <intrin.h>
#pragma intrinsic(_umul128)
#endif

namespace Microsoft::MixedReality::Sharing {
namespace {

constexpr uint64_t _wyp0 = 0xa0761d6478bd642full;
constexpr uint64_t _wyp1 = 0xe7037ed1a0b428dbull;
constexpr uint64_t _wyp2 = 0x8ebc6af09c88c6e3ull;
constexpr uint64_t _wyp3 = 0x589965cc75374cc3ull;
constexpr uint64_t _wyp4 = 0x1d8e4e27c47d124full;

inline uint64_t _wymum(uint64_t A, uint64_t B) {
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
}

inline uint64_t _wymix0(uint64_t A, uint64_t B, uint64_t seed) {
  return _wymum(A ^ seed ^ _wyp0, B ^ seed ^ _wyp1);
}

inline uint64_t _wymix1(uint64_t A, uint64_t B, uint64_t seed) {
  return _wymum(A ^ seed ^ _wyp2, B ^ seed ^ _wyp3);
}

inline uint64_t _wyr08(const uint8_t* p) {
  uint8_t v;
  memcpy(&v, p, 1);
  return v;
}

inline uint64_t _wyr16(const uint8_t* p) {
  uint16_t v;
  memcpy(&v, p, 2);
  return v;
}

inline uint64_t _wyr32(const uint8_t* p) {
  uint32_t v;
  memcpy(&v, p, 4);
  return v;
}

inline uint64_t _wyr64(const uint8_t* p) {
  uint64_t v;
  memcpy(&v, p, 8);
  return v;
}

inline uint64_t __wyr64(const uint8_t* p) {
  return (_wyr32(p) << 32) | _wyr32(p + 4);
}

}  // namespace

uint64_t CalculateHash64(const char* data,
                         size_t size,
                         uint64_t seed) noexcept {
  const uint8_t* p = reinterpret_cast<const uint8_t*>(data);
  size_t len1 = size;
  for (size_t i = 0; i + 32 <= size; i += 32, p += 32)
    seed = _wymix0(_wyr64(p), _wyr64(p + 8), seed) ^
           _wymix1(_wyr64(p + 16), _wyr64(p + 24), seed);
  switch (size & 31) {
    case 0:
      len1 = _wymix0(len1, 0, seed);
      break;
    case 1:
      seed = _wymix0(_wyr08(p), 0, seed);
      break;
    case 2:
      seed = _wymix0(_wyr16(p), 0, seed);
      break;
    case 3:
      seed = _wymix0((_wyr16(p) << 8) | _wyr08(p + 2), 0, seed);
      break;
    case 4:
      seed = _wymix0(_wyr32(p), 0, seed);
      break;
    case 5:
      seed = _wymix0((_wyr32(p) << 8) | _wyr08(p + 4), 0, seed);
      break;
    case 6:
      seed = _wymix0((_wyr32(p) << 16) | _wyr16(p + 4), 0, seed);
      break;
    case 7:
      seed = _wymix0((_wyr32(p) << 24) | (_wyr16(p + 4) << 8) | _wyr08(p + 6),
                     0, seed);
      break;
    case 8:
      seed = _wymix0(__wyr64(p), 0, seed);
      break;
    case 9:
      seed = _wymix0(__wyr64(p), _wyr08(p + 8), seed);
      break;
    case 10:
      seed = _wymix0(__wyr64(p), _wyr16(p + 8), seed);
      break;
    case 11:
      seed =
          _wymix0(__wyr64(p), (_wyr16(p + 8) << 8) | _wyr08(p + 8 + 2), seed);
      break;
    case 12:
      seed = _wymix0(__wyr64(p), _wyr32(p + 8), seed);
      break;
    case 13:
      seed =
          _wymix0(__wyr64(p), (_wyr32(p + 8) << 8) | _wyr08(p + 8 + 4), seed);
      break;
    case 14:
      seed =
          _wymix0(__wyr64(p), (_wyr32(p + 8) << 16) | _wyr16(p + 8 + 4), seed);
      break;
    case 15:
      seed = _wymix0(
          __wyr64(p),
          (_wyr32(p + 8) << 24) | (_wyr16(p + 8 + 4) << 8) | _wyr08(p + 8 + 6),
          seed);
      break;
    case 16:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed);
      break;
    case 17:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(_wyr08(p + 16), 0, seed);
      break;
    case 18:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(_wyr16(p + 16), 0, seed);
      break;
    case 19:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1((_wyr16(p + 16) << 8) | _wyr08(p + 16 + 2), 0, seed);
      break;
    case 20:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(_wyr32(p + 16), 0, seed);
      break;
    case 21:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1((_wyr32(p + 16) << 8) | _wyr08(p + 16 + 4), 0, seed);
      break;
    case 22:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1((_wyr32(p + 16) << 16) | _wyr16(p + 16 + 4), 0, seed);
      break;
    case 23:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1((_wyr32(p + 16) << 24) | (_wyr16(p + 16 + 4) << 8) |
                         _wyr08(p + 16 + 6),
                     0, seed);
      break;
    case 24:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16), 0, seed);
      break;
    case 25:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16), _wyr08(p + 24), seed);
      break;
    case 26:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16), _wyr16(p + 24), seed);
      break;
    case 27:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16),
                     (_wyr16(p + 24) << 8) | _wyr08(p + 24 + 2), seed);
      break;
    case 28:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16), _wyr32(p + 24), seed);
      break;
    case 29:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16),
                     (_wyr32(p + 24) << 8) | _wyr08(p + 24 + 4), seed);
      break;
    case 30:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16),
                     (_wyr32(p + 24) << 16) | _wyr16(p + 24 + 4), seed);
      break;
    case 31:
      seed = _wymix0(__wyr64(p), __wyr64(p + 8), seed) ^
             _wymix1(__wyr64(p + 16),
                     (_wyr32(p + 24) << 24) | (_wyr16(p + 24 + 4) << 8) |
                         _wyr08(p + 24 + 6),
                     seed);
      break;
  }
  return _wymum(seed ^ len1, _wyp4);
}

uint64_t CalculateHash64(uint64_t value_a, uint64_t value_b) noexcept {
  return _wymum(_wymum(value_a ^ _wyp0, value_b ^ _wyp1), _wyp2);
}

}  // namespace Microsoft::MixedReality::Sharing
