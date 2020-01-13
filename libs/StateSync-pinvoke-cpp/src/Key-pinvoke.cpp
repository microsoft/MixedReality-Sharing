// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/bit_cast.h>
#include <Microsoft/MixedReality/Sharing/StateSync/Key.h>
#include <Microsoft/MixedReality/Sharing/StateSync/export.h>
#include <Microsoft/MixedReality/Sharing/VersionedStorage/Detail/layout.h>

using namespace Microsoft::MixedReality::Sharing;
using namespace Microsoft::MixedReality::Sharing::StateSync;

extern "C" {

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Key_AddRef(intptr_t handle) noexcept {
  if (auto* key = bit_cast<const Key*>(handle))
    key->AddRef();
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Key_RemoveRef(
    intptr_t handle) noexcept {
  if (auto* key = bit_cast<const Key*>(handle))
    key->RemoveRef();
}

MS_MR_SHARING_STATESYNC_API const char* MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Key_view(intptr_t handle,
                                                  int* out_size) noexcept {
  if (auto* key = bit_cast<const Key*>(handle)) {
    auto view = key->view();
    // TODO: ensure that we never allow keys larger than INT_MAX
    assert(view.size() < static_cast<size_t>(std::numeric_limits<int>::max()));
    *out_size = static_cast<int>(view.size());
    return view.data();
  }
  // Leaves out_size untouched.
  return nullptr;
}

MS_MR_SHARING_STATESYNC_API uint64_t MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Key_hash(intptr_t handle) noexcept {
  if (auto* key = bit_cast<const Key*>(handle))
    return key->hash();
  return 0;
}

MS_MR_SHARING_STATESYNC_API intptr_t MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Key_Create(const char* data,
                                                    int size) noexcept {
  return bit_cast<intptr_t>(
      Key::Create({data, static_cast<size_t>(size)}).release());
}

MS_MR_SHARING_STATESYNC_API intptr_t MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_Key_ExtractFromWrapper(
    VersionedStorage::Detail::KeyHandleWrapper* key_handle_wrapper) noexcept {
  return static_cast<intptr_t>(key_handle_wrapper->key_);
}

}  // extern "C"
