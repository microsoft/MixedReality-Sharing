// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Represents a snapshot of the storage in time, use <see cref="SearchKey"/> or <see cref="KeyRef"/> with this to access values in the storage usinh.
    /// </summary>
    public class Snapshot : DisposablePointerBase
    {
        private readonly int keyCount;

        /// <summary>
        /// Gets the <see cref="VersionedStorage"/> associated with this snapshot.
        /// </summary>
        public VersionedStorage Storage { get; }

        /// <summary>
        /// Gets the version of this snapshot.
        /// </summary>
        public ulong Version { get; }

        /// <summary>
        /// Gets the keys contained within this snapshot.
        /// </summary>
        public KeyCollection Keys => new KeyCollection(Pointer, keyCount);

        internal Snapshot(IntPtr snapshotPtr, VersionedStorage storage, ulong version, int keyCount)
            : base(snapshotPtr)
        {
            Storage = storage;
            Version = version;
            this.keyCount = keyCount;
        }

        /// <summary>
        /// Gets the binary value looked up by a combination of key and a subkey.
        /// </summary>
        /// <param name="key">The key to lookup with.</param>
        /// <param name="subkey">The subkey to lookup with.</param>
        /// <param name="readOnlySpan">Stores the value in the out parameter if succesful.</param>
        /// <returns>True if there is a value associated with key/subkey, otherwise false.</returns>
        public bool TryGetValue(KeyRef key, ulong subkey, out ReadOnlySpan<byte> readOnlySpan)
        {
            // TODO: This class is about to be replaced, this is a temporary stub
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks if the key/subkey pair exists in this snapshot.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <param name="subkey">The subkey to check for.</param>
        /// <returns>True if there is an entry for the key/subkey.</returns>
        public bool Contains(KeyRef key, ulong subkey)
        {
            // TODO: This class is about to be replaced, this is a temporary stub
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets all the subkeys associated with a key.
        /// </summary>
        /// <param name="key">The key to check against.</param>
        /// <returns>The span of subkeys associated with the key, empty if none.</returns>
        public SubkeyValueCollection GetSubkeys(KeyRef key)
        {
            // TODO: This class is about to be replaced, this is a temporary stub
            throw new NotImplementedException();
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
