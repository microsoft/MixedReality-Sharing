// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/Common/Blob.h>

#include <string_view>

using namespace Microsoft::MixedReality::Sharing;
using namespace std::literals;

TEST(Blob, preserves_content) {
  static constexpr auto content = "abc\0\1\2\255\255\255"sv;
  auto blob = Blob::Create(content);
  ASSERT_EQ(content, blob->view());
}

TEST(Blob, GetFromSharedViewDataPtr) {
  auto blob = Blob::Create("foo"sv);

  auto view = blob->view();

  // Method for PInvoke, shouldn't be called by the users.
  auto blob2 = blob->GetFromSharedViewDataPtr(view.data());

  ASSERT_EQ(blob.get(), blob.get());
}
