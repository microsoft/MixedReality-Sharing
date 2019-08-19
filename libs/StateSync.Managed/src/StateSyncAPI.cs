// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public delegate void TransactionCommitCallback(TransactionResult result);

    internal static class StateSyncAPI
    {
        internal static IntPtr VersionedStorage_Allocate(/*Some parameters*/)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr VersionedStorage_Release(IntPtr storagePointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Subscription_Allocate(IntPtr storagePointer, IntPtr keyPointer, IKeySubscription subscription)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Subscription_Allocate(IntPtr storagePointer, IntPtr keyPointer, ulong subKey, ISubKeySubscription subKeySubscription)
        {
            throw new NotImplementedException();
        }

        internal static void Subscription_Release(IntPtr subscriptionPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr SearchKey_Allocate(string keyValue)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr SearchKey_Allocate(IntPtr refKeyPointer)
        {
            throw new NotImplementedException();
        }

        internal static void SearchKey_Release(IntPtr intPtr)
        {
            throw new NotImplementedException();
        }

        internal static int SearchKey_GetHasCode(IntPtr intPtr)
        {
            throw new NotImplementedException();
        }

        internal static string SearchKey_ToString(IntPtr intPtr)
        {
            throw new NotImplementedException();
        }

        // In C#, the pointer value may come from either the one held by RefKey or SearchKey
        internal static bool SearchKey_Equals(IntPtr firstKey, IntPtr secondKey)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Snapshot_Allocate(IntPtr storagePointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Snapshot_Release(IntPtr snapshotPointer)
        {
            throw new NotImplementedException();
        }

        internal static ReadOnlySpan<byte> Snapshot_Get(IntPtr snapshotPointer, IntPtr searchKeyPointer, ulong subKey)
        {
            throw new NotImplementedException();
        }

        internal static bool Snapshot_Contains(IntPtr snapshotPointer, IntPtr searchKeyPointer)
        {
            throw new NotImplementedException();
        }

        internal static ReadOnlySpan<ulong> Snapshot_GetSubKeys(IntPtr snapshotPointer, IntPtr searchKeyPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Transaction_Allocate(IntPtr snapshotPointer)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Set(IntPtr transactionPointer, IntPtr searchKeyPointer, ulong subKey, ReadOnlySpan<byte> value)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Clear(IntPtr transactionPointer, IntPtr searchKeyPointer, ulong subKey)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Require(IntPtr transactionPointer, IntPtr searchKeyPointer)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Commit(IntPtr transactionPointer, TransactionCommitCallback callback)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Release(IntPtr transactionPointer)
        {
            throw new NotImplementedException();
        }
    }
}
