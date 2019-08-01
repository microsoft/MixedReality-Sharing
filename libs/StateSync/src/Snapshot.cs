// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public ref struct LightweightSnapshot
    {
        private bool isDisposed;
        private readonly SynchronizationStore synchronizationStore;

        public LightweightSnapshot(SynchronizationStore synchronizationStore)
        {
            isDisposed = false;
            this.synchronizationStore = synchronizationStore;
        }

        public T Get<T>(SynchronizationKey key) where T : struct
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(LightweightSnapshot));
            }

            return synchronizationStore.Get<T>(this, key.KeyValue.Span, key.HashCode);
        }
        
        public T Get<T>(LightweightKey key)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(LightweightSnapshot));
            }

            return synchronizationStore.Get<T>(this, key.KeyValue, key.HashCode);
        }

        public Transaction CreateTransaction()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(LightweightSnapshot));
            }

            return new Transaction(synchronizationStore, this);
        }

        //C# 8 will call this automatically with "using"
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            synchronizationStore.ReleaseSnapshot(this);
        }
    }
}
