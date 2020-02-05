// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Blob.h>
#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>
#include <Microsoft/MixedReality/Sharing/Common/bit_cast.h>
#include <Microsoft/MixedReality/Sharing/StateSync/export.h>

#include <cstring>
#include <type_traits>

using namespace Microsoft::MixedReality::Sharing;

extern "C" {

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_VirtualRefCountedBase_RemoveRef(
    intptr_t handle) noexcept {
  bit_cast<VirtualRefCountedBase*>(handle)->RemoveRef();
}

}  // extern "C"
