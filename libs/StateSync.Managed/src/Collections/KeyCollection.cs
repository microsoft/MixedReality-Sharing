// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Holds a collecction of keys.
    /// </summary>
    public ref struct KeyCollection
    {
        private readonly IntPtr snapshotPointer;

        /// <summary>
        /// The number of keys in the collection.
        /// </summary>
        public int Count { get; }

        internal KeyCollection(IntPtr snapshotPointer, int count)
        {
            this.snapshotPointer = snapshotPointer;
            Count = count;
        }

        /// <summary>
        /// Gets an enumeration over this collection.
        /// </summary>
        /// <returns>The key enumerator.</returns>
        public KeyEnumerator GetEnumerator()
        {
            return new KeyEnumerator(StateSyncAPI.KeyEnumerator_Allocate(snapshotPointer));
        }
    }
}