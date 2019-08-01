// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public enum TransactionResult
    {
        Succeeded,
        Conflict,
        Failed
    }

    public class Transaction : DisposableBase, IDisposable
    {
        private readonly SynchronizationStore synchronizationStore;

        //public Snapshot Snapshot { get; }

        public Transaction(SynchronizationStore synchronizationStore, LightweightSnapshot snapshot)
        {
            this.synchronizationStore = synchronizationStore;
            //Snapshot = snapshot;
        }

        public void Require(SynchronizationKey key)
        {

        }

        public void Set<T>(SynchronizationKey key, T value) where T : struct
        {

        }

        public Task<TransactionResult> CommitAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(TransactionResult.Succeeded);
        }
    }
}
