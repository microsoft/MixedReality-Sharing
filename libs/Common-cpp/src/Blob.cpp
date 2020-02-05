// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Blob.h>
#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

#include <algorithm>
#include <cstddef>
#include <memory>

namespace Microsoft::MixedReality::Sharing {

MS_MR_SHARING_FORCEINLINE
Blob::Blob(const char* data, int size) noexcept : size_{size} {
  if (size != 0) {
    memcpy(data_, data, size);
  }
}

void Blob::RemoveRef() const noexcept {
  if (ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
    void* const raw_ptr = const_cast<Blob*>(this);
    this->~Blob();
    free(raw_ptr);
  }
}

MS_MR_SHARING_FORCEINLINE
RefPtr<const Blob> Blob::CreateImpl(const char* data, int size) noexcept {
  assert(size >= 0);
  const size_t alloc_size =
      std::max(sizeof(Blob), offsetof(Blob, data_) + static_cast<size_t>(size));
  void* raw_ptr = malloc(alloc_size);
  Blob* key_ptr = new (raw_ptr) Blob{data, size};
  return RefPtr<Blob>{key_ptr, DontAddRef{}};  // ref_count_ starts at 1.
}

RefPtr<const Blob> Blob::Create(const char* data, size_t size) {
  if (size > std::numeric_limits<int>::max())
    throw std::invalid_argument(
        "Can't create a blob larger than 2147483647 bytes");
  return CreateImpl(data, static_cast<int>(size));
}

RefPtr<const Blob> Blob::Create(const char* data, int size) {
  if (size < 0)
    throw std::invalid_argument("Can't create a blob with negative size");
  return CreateImpl(data, size);
}

RefPtr<const Blob> Blob::GetFromSharedViewDataPtr(
    const char* valid_data_ptr) noexcept {
  assert(valid_data_ptr);
  return {const_cast<Blob*>(
      reinterpret_cast<const Blob*>(valid_data_ptr - offsetof(Blob, data_)))};
}

}  // namespace Microsoft::MixedReality::Sharing
