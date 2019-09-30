// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license
// information.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

// TODO: implement for non-Windows platforms
#include <Microsoft/MixedReality/Sharing/Common/windows.h>

namespace Microsoft::MixedReality::Sharing::Platform {

void* AllocateZeroedPages(size_t pages_count) {
  return VirtualAlloc(NULL, pages_count * kPageSize, MEM_COMMIT,
                      PAGE_READWRITE);
}

void FreePages(void* address) {
  VirtualFree(address, 0, MEM_RELEASE);
}

}  // namespace Microsoft::MixedReality::Sharing::Platform
