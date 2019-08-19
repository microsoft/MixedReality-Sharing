// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An allocated search key instance that can be used to associated values in storage with.
    /// </summary>
    public class SearchKey : DisposablePointerBase, IEquatable<SearchKey>
    {
        /// <summary>
        /// Creates a <see cref="SearchKey"/> given a <see cref="string"/> key value.
        /// </summary>
        /// <param name="keyValue">The value for the search key.</param>
        /// <remarks>
        /// Multiple calls to this method with the same string key value will return equivalent but different instance search keys.
        /// You must manage it's lifetime and dispose of it when finished.
        /// </remarks>
        public static SearchKey Create(string keyValue)
        {
            return new SearchKey(StateSyncAPI.SearchKey_Allocate(keyValue));
        }

        internal SearchKey(IntPtr searchKeyPtr)
            : base(searchKeyPtr)
        {
        }

        /// <summary>
        /// Checks whether the current search key is equal to the given one.
        /// </summary>
        /// <param name="other">The other search key.</param>
        /// <returns>True if this key equals the given one, otherwise false.</returns>
        public bool Equals(SearchKey other)
        {
            ThrowIfDisposed();
            other.ThrowIfDisposed();

            return StateSyncAPI.SearchKey_Equals(Pointer, other.Pointer);
        }

        /// <summary>
        /// Checks whether the current search key is equal to the given one.
        /// </summary>
        /// <param name="other">The other search key.</param>
        /// <returns>True if this key equals the given one, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            return obj is SearchKey other && Equals(other);
        }

        public override string ToString()
        {
            ThrowIfDisposed();

            return StateSyncAPI.SearchKey_ToString(Pointer);
        }

        public override int GetHashCode()
        {
            ThrowIfDisposed();

            return StateSyncAPI.SearchKey_GetHasCode(Pointer);
        }

        protected override void ReleasePointer(IntPtr pointer)
        {
            StateSyncAPI.SearchKey_Release(pointer);
        }
    }
}
