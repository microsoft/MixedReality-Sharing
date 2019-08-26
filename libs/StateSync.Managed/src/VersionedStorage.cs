// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Versioned storage to use for synchronizing values.
    /// </summary>
    public class VersionedStorage : DisposablePointerBase
    {
        /// <summary>
        /// Creates a new instance of the storage.
        /// </summary>
        /// <returns>A new instance of the storage.</returns>
        public static VersionedStorage Create(/*SOME PARAMS THAT WILL BE NEEDED*/)
        {
            return new VersionedStorage(StateSyncAPI.VersionedStorage_Allocate());
        }

        private VersionedStorage(IntPtr storagePointer)
            : base(storagePointer)
        {
        }

        /// <summary>
        /// Gets the latest snapshot of the storage.
        /// </summary>
        /// <returns>A snapshot representing current state of the storage.</returns>
        public Snapshot GetSnapshot()
        {
            ThrowIfDisposed();

            (IntPtr pointer, ulong version, int keyCount) = StateSyncAPI.Snapshot_Allocate(Pointer);
            return new Snapshot(pointer, this, version, keyCount);
        }

        /// <summary>
        /// Registers a key subscription to the storage.
        /// </summary>
        /// <param name="key">The key for which to register the subscription.</param>
        /// <param name="subscription">The subscription instance.</param>
        /// <returns>A token to unregister the subscription.</returns>
        public SubscriptionToken SubscribeToKey(KeyRef key, IKeySubscription subscription)
        {
            ThrowIfDisposed();

            return new SubscriptionToken(StateSyncAPI.Subscription_Allocate(Pointer, key.Pointer, subscription));
        }

        /// <summary>
        /// Registers a key and subkey subscription to the storage.
        /// </summary>
        /// <param name="key">The key for which to register the subscription.</param>
        /// <param name="subkey">The subkey for which to register the subscription.</param>
        /// <param name="subscription">The subscription instance.</param>
        /// <returns>A token to unregister the subscription.</returns>
        public SubscriptionToken SubscribeToKey(KeyRef key, ulong subkey, ISubkeySubscription subscription)
        {
            ThrowIfDisposed();

            return new SubscriptionToken(StateSyncAPI.Subscription_Allocate(Pointer, key.Pointer, subkey, subscription));
        }

        protected override void ReleasePointer(IntPtr pointer)
        {
            StateSyncAPI.VersionedStorage_Release(pointer);
        }
    }
}
