// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Enumerates <see cref="KeySnapshot"/> objects in a <see cref="StateSnapshot"/>.
    /// </summary>
    public ref struct KeySnapshotEnumerator
    {
        /// <summary>
        /// Observed snapshot.
        /// </summary>
        public readonly StateSnapshot Snapshot;

        // TODO: this is a placeholder member.
        // The plumbing on C++ side is not done yet, but it's going to be similar
        // to what's going on in SubkeySnapshotEnumerator.
        // Basically a ref struct mimics internal C++ struct on the other side,
        // and the PInvoke methods are directly modifying the C#'s ref struct.
        // This means that creating an enumerator doesn't involve any allocations
        // on either side. It's just some memory on the stack.
        private IntPtr _internalHandle;

        /// <summary>
        /// Returns the <see cref="KeySnapshot"/> at the current position of the enumerator.
        /// </summary>
        public KeySnapshot Current { get { throw new NotImplementedException(); }
         }

        /// <summary>
        /// Moves to next item in the enumeration.
        /// </summary>
        /// <returns>True if there is a next item.</returns>
        public bool MoveNext() { throw new NotImplementedException(); }

        internal KeySnapshotEnumerator(StateSnapshot snapshot, IntPtr internalHandle)
        {
            Snapshot = snapshot;
            this._internalHandle = internalHandle;
        }
    }
}