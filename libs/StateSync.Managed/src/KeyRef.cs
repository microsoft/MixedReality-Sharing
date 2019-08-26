// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight unallocated key that can be used to lookup values in the storage. 
    /// This key can be convered to an allocated SearchKey, <see cref="AsSearchKey"/>.
    /// </summary>
    public ref struct KeyRef
    {
        public static implicit operator KeyRef(SearchKey key)
        {
            return new KeyRef(key.Pointer);
        }

        internal IntPtr Pointer { get; }

        internal KeyRef(IntPtr refKeyPtr)
        {
            Pointer = refKeyPtr;
        }

        /// <summary>
        /// Converts the current an allocated <see cref="SearchKey"/>.
        /// </summary>
        /// <returns>The created <see cref="SearchKey"/>.</returns>
        public SearchKey AsSearchKey()
        {
            return new SearchKey(StateSyncAPI.SearchKey_Allocate(Pointer));
        }

        public override bool Equals(object obj)
        {
            throw new NotSupportedException("Convert this to a SynchronizationKey first by calling AsSynchronizationKey().");
        }

        public override string ToString()
        {
            throw new NotSupportedException("Convert this to a SynchronizationKey first by calling AsSynchronizationKey().");
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException("Convert this to a SynchronizationKey first by calling AsSynchronizationKey().");
        }
    }
}
