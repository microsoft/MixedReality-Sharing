// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Enumerates the subkey/value pairs of a snapshot.
    /// </summary>
    public ref struct SubkeyValueEnumerator
    {
        private IntPtr enumeratorPointer;

        /// <summary>
        /// Gets the current value.
        /// </summary>
        public SubkeyValuePair Current { get; private set; }

        internal SubkeyValueEnumerator(IntPtr enumeratorPointer)
        {
            this.enumeratorPointer = enumeratorPointer;
            Current = default;
        }

        /// <summary>
        /// Moves to next item in the enumeration.
        /// </summary>
        /// <returns>True if there is a next item.</returns>
        public bool MoveNext()
        {
            if (StateSyncAPI.SubkeyEnumerator_MoveNext(enumeratorPointer))
            {
                (ulong subkey, IntPtr valuePointer, int length) = StateSyncAPI.SubkeyEnumerator_Current(enumeratorPointer);
                Current = new SubkeyValuePair(subkey, valuePointer, length);
                return true;
            }
            // else

            return false;
        }

        /// <summary>
        /// Disposes of the enumerator.
        /// </summary>
        public void Dispose()
        {
            StateSyncAPI.SubkeyEnumerator_Release(enumeratorPointer);
            enumeratorPointer = IntPtr.Zero;
        }
    }
}
