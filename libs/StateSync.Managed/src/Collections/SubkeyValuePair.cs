// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A simple structure that holds the subkey/value pair
    /// </summary>
    public struct SubkeyValuePair
    {
        private readonly IntPtr valueBeginPtr;
        private readonly int valueLength;

        /// <summary>
        /// Gets the subkey.
        /// </summary>
        public ulong Subkey { get; }

        /// <summary>
        /// Gets the value for that subkey.
        /// </summary>
        public unsafe ReadOnlySpan<byte> Value => new ReadOnlySpan<byte>(valueBeginPtr.ToPointer(), valueLength);

        internal SubkeyValuePair(ulong subkey, IntPtr valueBeginPtr, int valueSize)
        {
            Subkey = subkey;
            this.valueBeginPtr = valueBeginPtr;
            valueLength = valueSize;
        }
    }
}
