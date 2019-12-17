// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Collection of subkey/value pairs.
    /// </summary>
    public ref struct SubkeyValueCollection
    {
        private readonly IntPtr snapshotPointer;
        private readonly IntPtr keyPointer;

        /// <summary>
        /// Number of pairs in the colleciton.
        /// </summary>
        public int Count { get; }

        internal SubkeyValueCollection(IntPtr snapshotPointer, IntPtr keyPointer, int count)
        {
            this.snapshotPointer = snapshotPointer;
            this.keyPointer = keyPointer;
            Count = count;
        }

        /// <summary>
        /// Gets an enumerator over the collection.
        /// </summary>
        /// <returns>A enumerator over this subkey/value pair collection.</returns>
        public SubkeyValueEnumerator GetEnumerator()
        {
            return new SubkeyValueEnumerator(StateSyncAPI.SubkeyEnumerator_Allocate(snapshotPointer, keyPointer));
        }
    }
}
