﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public enum TransactionResult
    {
        Succeeded,
        Conflict,
        Failed
    }

    /// <summary>
    /// Transactions are used to mutate the storage, adding/updated/removing values for keys.
    /// </summary>
    public class Transaction : DisposablePointerBase
    {
        public Transaction(IntPtr transactionPointer)
            : base(transactionPointer)
        {
        }

        /// <summary>
        /// Specifies a key as an immutable constraints in order for this transaction to be commited succesfully.
        /// If the value associated with key/subkey changes, this transaction will be rejected.
        /// </summary>
        /// <param name="key">The required key.</param>
        /// <param name="subkey">The required subkey.</param>
        public void Require(KeyRef key, ulong subkey)
        {
            // TODO: This class is about to be replaced, this is a temporary stub
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds a value to be applied to the storage with a given key and subkey combination.
        /// </summary>
        /// <param name="key">Key to associate the value with.</param>
        /// <param name="subkey">Subkey to associate the value with.</param>
        /// <param name="value">The binary value.</param>
        public void Put(KeyRef key, ulong subkey, ReadOnlySpan<byte> value)
        {
            // TODO: This class is about to be replaced, this is a temporary stub
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a value associated wiht a key and subkey.
        /// </summary>
        /// <param name="key">Key for which to remove the associated value.</param>
        /// <param name="subkey">Subkey for which to remove the associated value.</param>
        public void Delete(KeyRef key, ulong subkey)
        {
            // TODO: This class is about to be replaced, this is a temporary stub
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously commit the transaction, returning the <see cref="TransactionResult"/> on completion.
        /// </summary>
        /// <returns>The awaitable task with result of the transaction.</returns>
        public async Task<TransactionResult> CommitAsync()
        {
            ThrowIfDisposed();

            TaskCompletionSource<TransactionResult> tcs = new TaskCompletionSource<TransactionResult>();

            StateSyncAPI.Transaction_Commit(Pointer, tcs.SetResult);

            return await tcs.Task;
        }

        protected override void ReleasePointer(IntPtr pointer)
        {
            ThrowIfDisposed();

            StateSyncAPI.Transaction_Release(pointer);
        }
    }
}
