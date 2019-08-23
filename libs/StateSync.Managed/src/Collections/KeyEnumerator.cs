// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An enumerator over <see cref="KeyRef"/>.
    /// </summary>
    public ref struct KeyEnumerator
    {
        private IntPtr enumeratorPointer;

        /// <summary>
        /// Gets the current <see cref="KeyRef"/> of this enumeration.
        /// </summary>
        public KeyRef Current { get; private set; }

        internal KeyEnumerator(IntPtr enumeratorPointer)
        {
            this.enumeratorPointer = enumeratorPointer;
            Current = default;
        }

        /// <summary>
        /// Attempts to move forward in the enumeration.
        /// </summary>
        /// <returns>True if was succesful, false if reached the end of the enumeration.</returns>
        public bool MoveNext()
        {
            if (StateSyncAPI.KeyEnumerator_MoveNext(enumeratorPointer))
            {
                Current = new KeyRef(StateSyncAPI.KeyEnumerator_Current(enumeratorPointer));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Disposes of this enumeration.
        /// </summary>
        public void Dispose()
        {
            StateSyncAPI.KeyEnumerator_Release(enumeratorPointer);
            enumeratorPointer = IntPtr.Zero;
        }
    }
}