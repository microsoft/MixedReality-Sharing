// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Enumerates <see cref="SubkeySnapshot"/> objects associated with a key in a snapshot.
    /// </summary>
    public ref struct SubkeySnapshotEnumerator
    {
        /// <summary>
        /// Observed snapshot.
        /// </summary>
        public readonly StateSnapshot Snapshot;

        private PInvokeAPI.SubkeyIteratorState _implState;
        private readonly unsafe void* _keyHandleWrapper;
        private bool _isBeforeFirst;
        private SubkeySnapshot _currentSubkeySnapshot;

        /// <summary>
        /// Returns the <see cref="SubkeySnapshot"/> at the current position of the enumerator.
        /// </summary>
        public SubkeySnapshot Current {
            get {
                if (_isBeforeFirst || !Constants.IsVersionValid(_currentSubkeySnapshot.Version))
                    throw new InvalidOperationException("SubkeyEnumerator is not in a valid state");
                return _currentSubkeySnapshot;
            }
        }
        
        /// <summary>
        /// Moves to next item in the enumeration.
        /// </summary>
        /// <returns>True if there is a next item.</returns>
        public unsafe bool MoveNext()
        {
            if (_isBeforeFirst)
            {
                _isBeforeFirst = false;
                return _implState.currentStateBlock != null;
            }
            if (_implState.currentStateBlock == null)
                return false;

            PInvokeAPI.RawSubkeyView subkey_view = new PInvokeAPI.RawSubkeyView();
            PInvokeAPI.SubkeyIteratorState_Advance(ref _implState, ref subkey_view);

            if (_implState.currentStateBlock == null)
                return false;

            _currentSubkeySnapshot = new SubkeySnapshot(Snapshot, subkey_view.subkey, subkey_view.version, subkey_view.data, subkey_view.IntSize);
            return true;
        }

        internal unsafe SubkeySnapshotEnumerator(StateSnapshot snapshot, IntPtr snapshotHandle, void* keyHandleWrapper)
        {
            Snapshot = snapshot;
            _implState = new PInvokeAPI.SubkeyIteratorState();
            this._keyHandleWrapper = keyHandleWrapper;
            _isBeforeFirst = true;
            PInvokeAPI.RawSubkeyView subkey_view = new PInvokeAPI.RawSubkeyView();
            PInvokeAPI.SubkeyIteratorState_Init(keyHandleWrapper, snapshotHandle, ref _implState, ref subkey_view);
            _currentSubkeySnapshot = new SubkeySnapshot(snapshot, subkey_view.subkey, subkey_view.version, subkey_view.data, subkey_view.IntSize);
        }
    }
}
