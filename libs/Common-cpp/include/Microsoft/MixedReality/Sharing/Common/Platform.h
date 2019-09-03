// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#pragma once
#include <cstdint>

#if defined(__x86_64__) || defined(_M_X64)
#define MS_MR_SHARING_PLATFORM_AMD64
#include <xmmintrin.h>
#elif defined(__arm64__) || defined(__aarch64__)
#define MS_MR_SHARING_PLATFORM_ARM64
#endif

namespace Microsoft::MixedReality::Sharing::Platform {

constexpr uint32_t kPageSize = 4096;

void* AllocateZeroedPages(size_t pages_count);

void FreePages(void* address);

inline void Prefetch(const void* address) noexcept {
#ifdef MS_MR_SHARING_PLATFORM_AMD64
  _mm_prefetch(static_cast<const char*>(address), _MM_HINT_T0);
#else
  __builtin_prefetch(address);
#endif
}

}  // namespace Microsoft::MixedReality::Sharing::Platform
