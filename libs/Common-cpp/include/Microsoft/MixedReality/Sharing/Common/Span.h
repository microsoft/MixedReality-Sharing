// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cassert>
#include <cstddef>
#include <type_traits>

namespace Microsoft::MixedReality::Sharing {

// This is a temporary placeholder for std::span
// (which is not available in C++17).
// It doesn't support all the features of std::span, and should be replaced when
// the project migrates to C++20.

template <typename T>
class Span {
 public:
  using element_type = T;
  using value_type = std::remove_cv_t<T>;
  using index_type = std::size_t;
  using difference_type = std::ptrdiff_t;
  using pointer = T*;
  using const_pointer = const T*;
  using reference = T&;
  using const_reference = const T&;
  using iterator = pointer;
  using const_iterator = const_pointer;

  constexpr Span() noexcept = default;
  constexpr Span(pointer ptr, index_type count) : data_(ptr), size_{count} {}
  constexpr Span(pointer first, pointer last)
      : data_{first}, size_{last - first} {}

  constexpr iterator begin() const noexcept { return data_; }
  constexpr const_iterator cbegin() const noexcept { return data_; }

  constexpr iterator end() const noexcept { return data_ + size_; }
  constexpr const_iterator cend() const noexcept { return data_ + size_; }

  reference front() const {
    assert(size_ > 0);
    return data_[0];
  }

  reference back() const {
    assert(size_ > 0);
    return data_[size_ - 1];
  }

  reference operator[](index_type idx) {
    assert(size_ > idx);
    return data_[idx];
  }

  constexpr pointer data() const noexcept { return data_; }
  constexpr index_type size() const noexcept { return size_; }
  constexpr index_type size_bytes() const noexcept { return size_ * sizeof(T); }
  [[nodiscard]] constexpr bool empty() const noexcept { return size_ != 0; }

 private:
  T* data_{nullptr};
  std::size_t size_{0};
};

template <typename T>
constexpr typename Span<T>::iterator begin(const Span<T>& span) noexcept {
  return span.begin();
}

template <typename T>
constexpr typename Span<T>::iterator end(const Span<T>& span) noexcept {
  return span.end();
}

}  // namespace Microsoft::MixedReality::Sharing
