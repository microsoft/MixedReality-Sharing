// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/bit_cast.h>
#include <Microsoft/MixedReality/Sharing/StateSync/Value.h>
#include <Microsoft/MixedReality/Sharing/StateSync/export.h>

using namespace Microsoft::MixedReality::Sharing;
using namespace Microsoft::MixedReality::Sharing::StateSync;

extern "C" {

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Value_AddRef(
    intptr_t handle) noexcept {
  if (auto* ptr = bit_cast<const Value*>(handle))
    ptr->AddRef();
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Value_RemoveRef(
    intptr_t handle) noexcept {
  if (auto* ptr = bit_cast<const Value*>(handle))
    ptr->RemoveRef();
}

MS_MR_SHARING_STATESYNC_API intptr_t MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Value_Create(const char* data,
                                                      int size) noexcept {
  return bit_cast<intptr_t>(
      Value::Create({data, static_cast<size_t>(size)}).release());
}

MS_MR_SHARING_STATESYNC_API const char* MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Value_view(intptr_t handle,
                                                    int* out_size) noexcept {
  if (auto* value = bit_cast<const Value*>(handle)) {
    auto view = value->view();
    // TODO: ensure that we never allow values larger than INT_MAX
    assert(view.size() < static_cast<size_t>(std::numeric_limits<int>::max()));
    *out_size = static_cast<int>(view.size());
    return view.data();
  }
  // Leaves out_size untouched.
  return nullptr;
}

}  // extern "C"
