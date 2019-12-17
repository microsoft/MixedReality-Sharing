// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/Value.h>

#include <string_view>

using namespace Microsoft::MixedReality::Sharing::StateSync;
using namespace std::literals;

TEST(Value, preserves_content) {
  static constexpr auto content = "abc\0\1\2\255\255\255"sv;
  auto value = Value::Create(content);
  ASSERT_EQ(content, value->view());
}

TEST(Value, GetFromSharedViewDataPtr) {
  auto value = Value::Create("foo"sv);

  auto view = value->view();

  // Method for PInvoke, shouldn't be called by the users.
  auto value2 = value->GetFromSharedViewDataPtr(view.data());

  ASSERT_EQ(value.get(), value2.get());
}
