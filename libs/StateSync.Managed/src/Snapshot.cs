// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Represents a snapshot of the storage in time, use <see cref="SearchKey"/> or <see cref="RefKey"/> with this to access values in the storage usinh.
    /// </summary>
    public class Snapshot : DisposablePointerBase
    {
        internal Snapshot(IntPtr snapshotPtr)
            : base(snapshotPtr)
        {
        }

        /// <summary>
        /// Gets the binary value looked up by a combination of key and a sub key.
        /// </summary>
        /// <param name="key">The key to lookup with.</param>
        /// <param name="subKey">The sub key to lookup with.</param>
        /// <returns>The associated value, if exists, otherwise an empty span.</returns>
        public ReadOnlySpan<byte> Get(RefKey key, ulong subKey)
        {
            ThrowIfDisposed();

            return StateSyncAPI.Snapshot_Get(Pointer, key.Pointer, subKey);
        }
        
        /// <summary>
        /// Checks whether the storage has any values associated with a key.
        /// </summary>
        /// <param name="key">The key to check against.</param>
        /// <returns>True if there is at least one entry associated with the key.</returns>
        public bool Contains(RefKey key)
        {
            ThrowIfDisposed();

            return StateSyncAPI.Snapshot_Contains(Pointer, key.Pointer);
        }
        
        /// <summary>
        /// Gets all the sub keys associated with a key.
        /// </summary>
        /// <param name="key">The key to check against.</param>
        /// <returns>The span of sub keys associated with the key, empty if none.</returns>
        public ReadOnlySpan<ulong> GetSubKeys(RefKey key)
        {
            ThrowIfDisposed();

            return StateSyncAPI.Snapshot_GetSubKeys(Pointer, key.Pointer);
        }

        /// <summary>
        /// Creates a new transaction based on the current snapshot.
        /// </summary>
        /// <returns>A new instance of a <see cref="Transaction"/>.</returns>
        public Transaction CreateTransaction()
        {
            ThrowIfDisposed();

            return new Transaction(StateSyncAPI.Transaction_Allocate(Pointer));
        }

        protected override void ReleasePointer(IntPtr pointer)
        {
            StateSyncAPI.Snapshot_Release(pointer);
        }
    }
}
