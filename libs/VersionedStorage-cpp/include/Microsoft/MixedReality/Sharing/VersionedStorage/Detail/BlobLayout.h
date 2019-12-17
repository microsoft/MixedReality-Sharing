// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cstddef>

namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail {

class IndexBlock;

struct BlobLayout {
  IndexBlock* index_begin_{nullptr};
  std::byte* data_begin_{nullptr};
};

}  // namespace Microsoft::MixedReality::Sharing::VersionedStorage::Detail
