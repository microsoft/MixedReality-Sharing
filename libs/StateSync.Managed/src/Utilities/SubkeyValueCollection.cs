// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Implementation of the <see cref="IReadOnlyCollection{SubkeyValuePair}"/>.
    /// </summary>
    internal class SubkeyValueCollection : IReadOnlyCollection<SubkeyValuePair>
    {
        private readonly Snapshot snapshot;
        private readonly IntPtr keyPointer;

        public int Count { get; }

        internal SubkeyValueCollection(Snapshot snapshot, IntPtr keyPointer, int count)
        {
            this.snapshot = snapshot;
            this.keyPointer = keyPointer;
            Count = count;
        }

        public IEnumerator<SubkeyValuePair> GetEnumerator()
        {
            snapshot.ThrowIfDisposed();

            IntPtr enumeratorPointer = StateSyncAPI.SubkeyEnumerator_Allocate(snapshot.Pointer, keyPointer);
            try
            {
                while (StateSyncAPI.SubkeyEnumerator_MoveNext(enumeratorPointer))
                {
                    (ulong subkey, IntPtr valuePointer, int length) = StateSyncAPI.SubkeyEnumerator_Current(enumeratorPointer);
                    yield return new SubkeyValuePair(subkey, valuePointer, length);
                }
            }
            finally
            {
                StateSyncAPI.SubkeyEnumerator_Release(enumeratorPointer);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


}
