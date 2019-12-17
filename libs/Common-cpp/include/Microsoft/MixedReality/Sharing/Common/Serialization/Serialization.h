// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/Common/Platform.h>

#include <stdexcept>

namespace Microsoft::MixedReality::Sharing::Serialization {

class BitstreamReader;
class BitstreamWriter;

#ifdef MS_MR_SHARING_PLATFORM_x86_OR_x64
using bit_shift_t = unsigned long;
#endif

}  // namespace Microsoft::MixedReality::Sharing::Serialization
