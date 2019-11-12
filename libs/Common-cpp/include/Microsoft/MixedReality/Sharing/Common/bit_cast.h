// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

#include <cassert>
#include <cstring>
#include <type_traits>

namespace Microsoft::MixedReality::Sharing {

// TODO: replace with std::bit_cast when it's avaialbe
template <class To, class From>
MS_MR_SHARING_FORCEINLINE
    typename std::enable_if_t<(sizeof(To) == sizeof(From)) &&
                                  std::is_trivially_copyable_v<From> &&
                                  std::is_trivial_v<To>,
                              To>
    bit_cast(const From& src) noexcept {
  To dst;
  memcpy(&dst, &src, sizeof(To));
  return dst;
}

// bit-casts a 64-bit enum to a pointer, dropping high bits if the pointer is
// smaller. Expected to be used on enums that are either 0-initialized or
// obtained by calling pointer_to_enum64().
template <class To, class From>
MS_MR_SHARING_FORCEINLINE
    typename std::enable_if_t<(sizeof(From) == 64) &&
                                  (sizeof(To) <= sizeof(From)) &&
                                  std::is_enum_v<From> && std::is_pointer_v<To>,
                              To>
    enum64_to_pointer(From src) noexcept {
  // Note: it is allowed to restore 32-bit pointers from 64-bit enums.
  To dst;
  memcpy(&dst, &src, sizeof(dst));
  return dst;
}

// bit-casts a pointer to a 64-bit enum, extending it with zeros if the pointer
// is smaller. Stored pointers can be retrieved with enum64_to_pointer().
template <class To, class From>
MS_MR_SHARING_FORCEINLINE
    typename std::enable_if_t<(sizeof(To) >= sizeof(From)) &&
                                  std::is_enum_v<To> && std::is_pointer_v<From>,
                              To>
    pointer_to_enum64(From src) noexcept {
  To dst{0};
  // Note: the enum can be larger than the pointer, in which case the top bits
  // will stay zeroed.
  memcpy(&dst, &src, sizeof(src));
  return dst;
}

}  // namespace Microsoft::MixedReality::Sharing
