// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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

    public interface ITransaction : IDisposable
    {
        Snapshot Snapshot { get; }

        //void Set(Key)

        Task<TransactionResult> CommitAsync(CancellationToken cancellationToken);
    }
}
