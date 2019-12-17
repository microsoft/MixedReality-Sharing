// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <atomic>

namespace Microsoft::MixedReality::Sharing {

class VirtualRefCountedBase {
 public:
  VirtualRefCountedBase() noexcept = default;

  void AddRef() const noexcept {
    ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  void RemoveRef() const noexcept {
    if (ref_count_.fetch_sub(1, std::memory_order_acq_rel) == 1)
      delete this;
  }

 protected:
  virtual ~VirtualRefCountedBase() noexcept = default;

 private:
  VirtualRefCountedBase(const VirtualRefCountedBase&) = delete;
  VirtualRefCountedBase& operator=(const VirtualRefCountedBase&) = delete;

  mutable std::atomic_uint32_t ref_count_{0};
};

}  // namespace Microsoft::MixedReality::Sharing
