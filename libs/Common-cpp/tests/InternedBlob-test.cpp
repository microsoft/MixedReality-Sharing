// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/Common/InternedBlob.h>

#include <string_view>

using namespace Microsoft::MixedReality::Sharing;
using namespace std::literals;

TEST(InternedBlob, preserves_content) {
  static constexpr auto content = "abc\0\1\2\255\255\255"sv;
  auto blob = InternedBlob::Create(content);
  ASSERT_EQ(content, blob->view());
}

TEST(InternedBlob, interning) {
  auto blob1 = InternedBlob::Create("foo"sv);
  auto blob2 = InternedBlob::Create("foo"sv);
  auto blob_other = InternedBlob::Create("bar"sv);

  ASSERT_EQ(blob1, blob2);
  ASSERT_EQ(blob1.get(), blob2.get());

  ASSERT_NE(blob1, blob_other);
  ASSERT_NE(blob1.get(), blob_other.get());
}

TEST(InternedBlob, ordering) {
  auto blob_a = InternedBlob::Create("a"sv);
  auto blob_ab = InternedBlob::Create("ab"sv);
  auto blob_aaa = InternedBlob::Create("aaa"sv);
  auto blob_aab = InternedBlob::Create("aab"sv);

  // Not less or greater than itself
  ASSERT_FALSE(blob_a->OrderedLess(*blob_a));
  ASSERT_FALSE(blob_a->OrderedLess("a"sv));
  ASSERT_FALSE(blob_a->OrderedGreater("a"sv));

  // Ordered length first
  ASSERT_TRUE(blob_a->OrderedLess("ab"sv));
  ASSERT_TRUE(blob_a->OrderedLess(*blob_ab));
  ASSERT_FALSE(blob_a->OrderedGreater("ab"sv));

  ASSERT_TRUE(blob_ab->OrderedLess(*blob_aaa));
  ASSERT_TRUE(blob_ab->OrderedLess(*blob_aab));
  ASSERT_FALSE(blob_ab->OrderedGreater("aaa"sv));
  ASSERT_FALSE(blob_ab->OrderedGreater("aab"sv));

  // If the length is the same, ordered lexicographically
  ASSERT_TRUE(blob_aaa->OrderedLess(*blob_aab));
  ASSERT_TRUE(blob_aaa->OrderedLess("aab"));
  ASSERT_FALSE(blob_aaa->OrderedGreater("aab"));
}
