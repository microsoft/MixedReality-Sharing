// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public static class Constants
    {
        // Versions greater or equal to this value are considered to be invalid.
        public const ulong kInvalidVersion = 0x7FFF_FFFF_FFFF_FFFF;

        public static bool IsVersionValid(ulong version)
        {
            return version < kInvalidVersion;
        }
    }
}
