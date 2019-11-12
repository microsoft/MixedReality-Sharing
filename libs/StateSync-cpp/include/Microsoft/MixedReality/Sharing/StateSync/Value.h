// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/RefPtr.h>

#include <atomic>
#include <cstdint>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class Value {
 public:
  static RefPtr<Value> Create(std::string_view data) noexcept;

  const char* data() const noexcept { return data_; }
  size_t size() const noexcept { return size_; }
  std::string_view view() const noexcept { return {data_, size_}; }

  void AddRef() const noexcept {
    ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  void RemoveRef() const noexcept;

  // Assuming valid_data_ptr points to the buffer of an existing Value,
  // returns an owning pointer to that Value.
  static RefPtr<Value> GetFromSharedViewDataPtr(
      const char* valid_data_ptr) noexcept;

 private:
  Value(const char* data, size_t size) noexcept;
  Value(const Value&) = delete;
  Value& operator=(const Value&) = delete;
  ~Value() noexcept = default;

  // ref_count_ starts with 1 because the only way to create the object is
  // through Create(), and it returns a RefPtr.
  mutable std::atomic_size_t ref_count_{1};
  const size_t size_;

#ifdef _MSC_VER
#pragma warning(push)
// nonstandard extension used: zero-sized array in struct/union
#pragma warning(disable : 4200)
#endif  // _MSC_VER

  char data_[];  // This is a language extension that is expected to be
                 // standardized in C++23, right now it happens to work
                 // in all the supported compiler.

#ifdef _MSC_VER
#pragma warning(pop)
#endif  // _MSC_VER

  friend class SubkeyView;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
