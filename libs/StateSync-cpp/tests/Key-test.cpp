// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/Key.h>

#include <string_view>

using namespace Microsoft::MixedReality::Sharing::StateSync;
using namespace std::literals;

TEST(Key, preserves_content) {
  static constexpr auto content = "abc\0\1\2\255\255\255"sv;
  auto key = Key::Create(content);
  ASSERT_EQ(content, key->view());
}

TEST(Key, interning) {
  auto key1 = Key::Create("foo"sv);
  auto key2 = Key::Create("foo"sv);
  auto key_other = Key::Create("bar"sv);

  ASSERT_EQ(key1, key2);
  ASSERT_EQ(key1.get(), key2.get());

  ASSERT_NE(key1, key_other);
  ASSERT_NE(key1.get(), key_other.get());
}

TEST(Key, ordering) {
  auto key_a = Key::Create("a"sv);
  auto key_ab = Key::Create("ab"sv);
  auto key_aaa = Key::Create("aaa"sv);
  auto key_aab = Key::Create("aab"sv);

  // Not less or greater than itself
  ASSERT_FALSE(key_a->OrderedLess(*key_a));
  ASSERT_FALSE(key_a->OrderedLess("a"sv));
  ASSERT_FALSE(key_a->OrderedGreater("a"sv));

  // Ordered length first
  ASSERT_TRUE(key_a->OrderedLess("ab"sv));
  ASSERT_TRUE(key_a->OrderedLess(*key_ab));
  ASSERT_FALSE(key_a->OrderedGreater("ab"sv));

  ASSERT_TRUE(key_ab->OrderedLess(*key_aaa));
  ASSERT_TRUE(key_ab->OrderedLess(*key_aab));
  ASSERT_FALSE(key_ab->OrderedGreater("aaa"sv));
  ASSERT_FALSE(key_ab->OrderedGreater("aab"sv));

  // If the length is the same, ordered lexicographically
  ASSERT_TRUE(key_aaa->OrderedLess(*key_aab));
  ASSERT_TRUE(key_aaa->OrderedLess("aab"));
  ASSERT_FALSE(key_aaa->OrderedGreater("aab"));
}
