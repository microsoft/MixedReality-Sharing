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

        internal static IntPtr Subscription_Allocate(IntPtr storagePointer, IntPtr keyPointer, ulong subkey, ISubkeySubscription subkeySubscription)
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

        // return (Ptr, Version)
        internal static (IntPtr, ulong) Snapshot_Allocate(IntPtr storagePointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Snapshot_Release(IntPtr snapshotPointer)
        {
            throw new NotImplementedException();
        }

        internal static bool Snapshot_TryGet(IntPtr snapshotPointer, IntPtr searchKeyPointer, ulong subkey, out ReadOnlySpan<byte> readOnlySpan)
        {
            throw new NotImplementedException();
        }

        internal static bool Snapshot_Contains(IntPtr snapshotPointer, IntPtr searchKeyPointer, ulong subkey)
        {
            throw new NotImplementedException();
        }

        internal static int Snapshot_GetSubkeyCount(IntPtr snapshotPointer, IntPtr searchKeyPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr Transaction_Allocate(IntPtr snapshotPointer)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Put(IntPtr transactionPointer, IntPtr searchKeyPointer, ulong subkey, ReadOnlySpan<byte> value)
        {
            throw new NotImplementedException();
        }

        internal static void Transaction_Delete(IntPtr transactionPointer, IntPtr searchKeyPointer, ulong subkey)
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

        internal static IntPtr SubkeyEnumerator_Allocate(IntPtr snapshotPointer, IntPtr keyPointer)
        {
            throw new NotImplementedException();
        }

        internal static bool SubkeyEnumerator_MoveNext(IntPtr enumeratorPointer)
        {
            throw new NotImplementedException();
        }

        internal static (ulong, IntPtr, int) SubkeyEnumerator_Current(IntPtr enumeratorPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr SubkeyEnumerator_Release(IntPtr enumeratorPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr KeyEnumerator_Allocate(IntPtr snapshotPointer)
        {
            throw new NotImplementedException();
        }

        internal static bool KeyEnumerator_MoveNext(IntPtr enumeratorPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr KeyEnumerator_Current(IntPtr enumeratorPointer)
        {
            throw new NotImplementedException();
        }

        internal static IntPtr KeyEnumerator_Release(IntPtr enumeratorPointer)
        {
            throw new NotImplementedException();
        }
    }
}
