// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/RefPtr.h>

#include <atomic>
#include <cstdint>
#include <limits>
#include <string_view>

namespace Microsoft::MixedReality::Sharing {

// A reference-counted immutable blob of bytes.
// The size of the contained data is limited to ~2Gb for easier integration with
// C#, where multiple data types, such as Span, use int for the Length property.
class Blob {
 public:
  static constexpr size_t kMaxSize =
      static_cast<size_t>(std::numeric_limits<int>::max());

  // Throws if the size is negative.
  static RefPtr<const Blob> Create(const char* data, int size);

  // Throws if the size doesn't fit into a signed int
  // (to make sure that the data is always observable as a Span in C#).
  static RefPtr<const Blob> Create(const char* data, size_t size);

  // Throws if the size of the span doesn't fit into a signed int
  // (to make sure that the data is always observable as a Span in C#).
  static RefPtr<const Blob> Create(std::string_view data) {
    return Create(data.data(), data.size());
  }

  const char* data() const noexcept { return data_; }
  size_t size() const noexcept { return static_cast<size_t>(size_); }
  int size_int() const noexcept { return size_; }
  std::string_view view() const noexcept { return {data_, size()}; }

  void AddRef() const noexcept {
    ref_count_.fetch_add(1, std::memory_order_relaxed);
  }

  void RemoveRef() const noexcept;

  // Assuming valid_data_ptr points to the buffer of an existing Blob,
  // returns an owning pointer to that Blob.
  static RefPtr<const Blob> GetFromSharedViewDataPtr(
      const char* valid_data_ptr) noexcept;

  // Should not be used in production code.
  uint32_t ref_count_for_testing() const noexcept {
    return ref_count_.load(std::memory_order_relaxed);
  }

 private:
  static RefPtr<const Blob> CreateImpl(const char* data, int size) noexcept;
  Blob(const char* data, int size) noexcept;
  Blob(const Blob&) = delete;
  Blob& operator=(const Blob&) = delete;
  ~Blob() noexcept = default;

  // ref_count_ starts with 1 because the only way to create the object is
  // through Create(), and it returns a RefPtr.
  mutable std::atomic_uint32_t ref_count_{1};
  const int size_;  // The type is int for compatibility with C#.

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

}  // namespace Microsoft::MixedReality::Sharing
