// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight structure that represents the state of a key in a snapshot.
    /// </summary>
    public readonly ref struct KeySnapshot
    {
        /// <summary>
        /// Observed snapshot.
        /// </summary>
        public readonly StateSnapshot Snapshot;

        /// <summary>
        /// Number of subkeys associated with this key in the observed snapshot.
        /// </summary>
        public readonly ulong SubkeysCount;

        /// <summary>
        /// Observed key.
        /// </summary>
        public unsafe InternedBlobRef Key
        {
            get { return new InternedBlobRef((IntPtr)_keyHandleWrapper->key_handle); }
        }

        private readonly unsafe PInvokeAPI.KeyHandleWrapper* _keyHandleWrapper;

        internal unsafe KeySnapshot(StateSnapshot snapshot, ulong subkeysCount, PInvokeAPI.KeyHandleWrapper* keyHandleWrapper)
        {
            Snapshot = snapshot;
            SubkeysCount = subkeysCount;
            _keyHandleWrapper = keyHandleWrapper;
        }

        /// <summary>
        /// Enumerates subkeys of this key in the observed snapshot.
        /// </summary>
        /// <returns>An enumerator over the collection of SubkeyView.</returns>
        public SubkeySnapshotEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
