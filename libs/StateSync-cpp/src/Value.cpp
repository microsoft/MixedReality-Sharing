// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/Value.h>

#include <algorithm>
#include <cstddef>
#include <memory>

namespace Microsoft::MixedReality::Sharing::StateSync {

inline Value::Value(const char* data, size_t size) noexcept : size_{size} {
  if (size) {
    memcpy(data_, data, size);
  }
}

void Value::RemoveRef() const noexcept {
  if (ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1) {
    void* const raw_ptr = const_cast<Value*>(this);
    this->~Value();
    free(raw_ptr);
  }
}

RefPtr<Value> Value::Create(std::string_view content) noexcept {
  const size_t alloc_size =
      std::max(sizeof(Value), offsetof(Value, data_) + content.size());
  void* raw_ptr = malloc(alloc_size);
  Value* key_ptr = new (raw_ptr) Value{content.data(), content.size()};
  return RefPtr<Value>{key_ptr, DontAddRef{}};  // ref_count_ starts at 1.
}

RefPtr<Value> Value::GetFromSharedViewDataPtr(
    const char* valid_data_ptr) noexcept {
  assert(valid_data_ptr);
  return {const_cast<Value*>(
      reinterpret_cast<const Value*>(valid_data_ptr - offsetof(Value, data_)))};
}

}  // namespace Microsoft::MixedReality::Sharing::StateSync
