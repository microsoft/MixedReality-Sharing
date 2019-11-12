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

template <class To, class From>
MS_MR_SHARING_FORCEINLINE
    typename std::enable_if_t<(sizeof(To) <= sizeof(From)) &&
                                  std::is_enum_v<From> && std::is_pointer_v<To>,
                              To>
    enum_to_pointer(From src) noexcept {
  // Note: it is allowed to restore 32-bit pointers from 64-bit enums.
  To dst;
  memcpy(&dst, &src, sizeof(dst));
  return dst;
}

template <class To, class From>
MS_MR_SHARING_FORCEINLINE
    typename std::enable_if_t<(sizeof(To) >= sizeof(From)) &&
                                  std::is_enum_v<To> && std::is_pointer_v<From>,
                              To>
    pointer_to_enum(From src) noexcept {
  To dst{0};
  // Note: the enum can be larger than the pointer, in which case the top bits
  // will stay zeroed.
  memcpy(&dst, &src, sizeof(src));
  return dst;
}

}  // namespace Microsoft::MixedReality::Sharing
