// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>
#include <Microsoft/MixedReality/Sharing/Common/bit_cast.h>
#include <Microsoft/MixedReality/Sharing/StateSync/Value.h>
#include <Microsoft/MixedReality/Sharing/StateSync/export.h>

#include <cstring>
#include <type_traits>

using namespace Microsoft::MixedReality::Sharing;
using namespace Microsoft::MixedReality::Sharing::StateSync;

extern "C" {

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_VirtualRefCountedBase_RemoveRef(
    intptr_t handle) noexcept {
  bit_cast<VirtualRefCountedBase*>(handle)->RemoveRef();
}

// Left for illustration purposes
/*
MS_MR_SHARING_STATESYNC_API intptr_t MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_SubkeyView_GetValue(const char* data) {
  return bit_cast<intptr_t>(Value::GetFromSharedViewDataPtr(data).release());
}
*/

}  // extern "C"
