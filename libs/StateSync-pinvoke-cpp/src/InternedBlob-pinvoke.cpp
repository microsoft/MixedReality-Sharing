// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/export.h>

#include <Microsoft/MixedReality/Sharing/Common/InternedBlob.h>
#include <Microsoft/MixedReality/Sharing/Common/bit_cast.h>

using namespace Microsoft::MixedReality::Sharing;

extern "C" {

MS_MR_SHARING_STATESYNC_API intptr_t MS_MR_CALL
Microsoft_MixedReality_Sharing_InternedBlob_Create(const char* data,
                                                   int size) noexcept {
  return bit_cast<intptr_t>(
      InternedBlob::Create({data, static_cast<size_t>(size)}).release());
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_InternedBlob_AddRef(intptr_t handle) noexcept {
  if (auto* blob = bit_cast<const InternedBlob*>(handle))
    blob->AddRef();
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_InternedBlob_RemoveRef(
    intptr_t handle) noexcept {
  if (auto* blob = bit_cast<const InternedBlob*>(handle))
    blob->RemoveRef();
}

MS_MR_SHARING_STATESYNC_API const char* MS_MR_CALL
Microsoft_MixedReality_Sharing_InternedBlob_view(intptr_t handle,
                                                 int* out_size) noexcept {
  if (auto* blob = bit_cast<const InternedBlob*>(handle)) {
    auto view = blob->view();
    // TODO: ensure that we never allow blobs larger than INT_MAX
    assert(view.size() < static_cast<size_t>(std::numeric_limits<int>::max()));
    *out_size = static_cast<int>(view.size());
    return view.data();
  }
  // Leaves out_size untouched.
  return nullptr;
}

MS_MR_SHARING_STATESYNC_API uint64_t MS_MR_CALL
Microsoft_MixedReality_Sharing_InternedBlob_hash(intptr_t handle) noexcept {
  if (auto* blob = bit_cast<const InternedBlob*>(handle))
    return blob->hash();
  return 0;
}

}  // extern "C"
