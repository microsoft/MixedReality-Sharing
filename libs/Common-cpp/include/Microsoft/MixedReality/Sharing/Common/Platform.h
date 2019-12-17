// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <cstdint>

#if defined(__x86_64__) || defined(_M_X64)
#define MS_MR_SHARING_PLATFORM_AMD64
#define MS_MR_SHARING_PLATFORM_ANY_64_BIT
#elif defined(__arm64__) || defined(__aarch64__)
#define MS_MR_SHARING_PLATFORM_ARM64
#define MS_MR_SHARING_PLATFORM_ANY_64_BIT
#elif defined(__i386__) || defined(_M_I86) || defined(_M_IX86) || defined(_X86_)
#define MS_MR_SHARING_PLATFORM_x86
#define MS_MR_SHARING_PLATFORM_ANY_32_BIT
#endif

#if defined(MS_MR_SHARING_PLATFORM_AMD64) || defined(MS_MR_SHARING_PLATFORM_x86)
#define MS_MR_SHARING_PLATFORM_x86_OR_x64
#endif

#ifdef MS_MR_SHARING_PLATFORM_x86_OR_x64
#include <xmmintrin.h>
#endif

#ifdef _MSC_VER
#define MS_MR_SHARING_FORCEINLINE __forceinline
#else
#define MS_MR_SHARING_FORCEINLINE __attribute__((always_inline))
#endif

namespace Microsoft::MixedReality::Sharing::Platform {

constexpr uint32_t kPageSize = 4096;

void* AllocateZeroedPages(size_t pages_count);

void FreePages(void* address);

inline void Prefetch(const void* address) noexcept {
#ifdef MS_MR_SHARING_PLATFORM_x86_OR_x64
  _mm_prefetch(static_cast<const char*>(address), _MM_HINT_T0);
#else
  __builtin_prefetch(address);
#endif
}

}  // namespace Microsoft::MixedReality::Sharing::Platform
