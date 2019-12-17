// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/RefPtr.h>

#include <atomic>
#include <cstdint>
#include <string_view>

namespace Microsoft::MixedReality::Sharing::StateSync {

class Key {
 public:
  // Returns an interned key with the copy of the provided content
  // (if a key with the same content already exists, a reference
  // to it will be returned).
  static RefPtr<Key> Create(std::string_view content) noexcept;

  constexpr const char* data() const noexcept { return data_; }
  constexpr size_t size() const noexcept { return size_; }
  constexpr uint64_t hash() const noexcept { return hash_; }
  constexpr std::string_view view() const noexcept { return {data_, size_}; }

  // Note: the ordering is not lexicographical.
  bool OrderedLess(const Key& other) const noexcept;
  bool OrderedLess(std::string_view sv) const noexcept;
  bool OrderedGreater(std::string_view sv) const noexcept;

  void AddRef() const noexcept {
    ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  void RemoveRef() const noexcept;

 private:
  Key(uint64_t hash, const char* data, size_t size) noexcept;
  Key(const Key&) = delete;
  Key& operator=(const Key&) = delete;
  ~Key() noexcept = default;

  const uint64_t hash_;
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
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
