// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An immutable representation of the <see cref="ReplicatedState"/> 
    /// at a specific moment, described by the snapshot's <see cref="Version"/>.
    /// </summary>
    /// <remarks>
    /// The snapshot will stay the same even if the <see cref="ReplicatedState"/>
    /// is mutated (and any change will advance the internal version of the
    /// <see cref="ReplicatedState"/> without affecting this snapshot).
    /// 
    /// Multiple snapshots (representing same or different versions of the state) can co-exist
    /// in the same application. Adding, modifying or deleting the keys and subkeys will not
    /// affect any existing snapshots.
    /// 
    /// The intended usage of the <see cref="ReplicatedState"/> is to obtain a snapshot
    /// at the beginning of a lengthy operation, and then work with it, assuming that it
    /// will always be consistent and immutable, regardless of what new transactions are
    /// will be applied to <see cref="ReplicatedState"/> after that.
    /// </remarks>
    /// 
    /// TODO: we may want a ref struct version of the same concept for callbacks (like with keys).
    public class StateSnapshot : Utilities.VirtualRefCountedBase
    {
        /// <summary>
        /// Returns the <see cref="ReplicatedState"/> associated with this snapshot.
        /// </summary>
        public readonly ReplicatedState State;

        /// <summary>
        /// The version of the <see cref="ReplicatedState"/> observable through this snapshot.
        /// </summary>
        public readonly ulong Version;

        /// <summary>
        /// The number of keys that contain any defined subkeys for this <see cref="Version"/>.
        /// </summary>
        public readonly ulong KeysCount;

        /// <summary>
        /// The number of subkeys (across all keys) that have defined values for this <see cref="Version"/>.
        /// </summary>
        public readonly ulong SubkeysCount;

        /// <summary>
        /// Returns an enumerator that iterates over <see cref="KeySnapshot"/> objects of this snapshot.
        /// </summary>
        /// <remarks>Keys exist in the version only when they have at least one subkey associated with them,
        /// therefore all <see cref="KeySnapshot"/> objects returned by enumerator will have at least one subkey.</remarks>
        public KeySnapshotEnumerator GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a <see cref="KeySnapshot"/> for the provided key.
        /// </summary>
        /// <remarks>The returned snapshot can be empty if the key doesn't exist in this snapshot.
        /// Use <see cref="KeySnapshot.SubkeysCount"/> to check if the key was found.</remarks>
        public KeySnapshot GetKey(KeyRef key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the number of subkeys associated with the key in this snapshot, or 0 if the key is missing.
        /// </summary>
        public ulong GetSubkeysCount(KeyRef key)
        {
            return PInvoke_GetSubkeysCount(handle, key.handle);
        }

        /// <summary>
        /// Returns true if the {key:subkey} pair exists in this snapshot.
        /// </summary>
        public bool Contains(KeyRef key, ulong subkey)
        {
            return PInvoke_Contains(handle, key.handle, subkey);
        }

        /// <summary>
        /// Returns the snapshot of the <paramref name="subkey"/> associated with the provided <paramref name="key"/>.
        /// </summary>
        /// <returns><see cref="SubkeySnapshot"/> object representing the state of the subkey.
        /// If the subkey doesn't exist in this snapshot, the returned <see cref="SubkeySnapshot"/> will be without a value.
        /// Use <see cref="SubkeySnapshot.HasValue"/> to check whether the subkey was found.</returns>
        public SubkeySnapshot TryGetValue(KeyRef key, ulong subkey)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets <paramref name="readOnlySpan"/> to the current binary value of the subkey.
        /// </summary>
        /// <returns>True if the subkey was found, and the <paramref name="readOnlySpan"/> now contains its value.
        /// False otherwise (in this case <paramref name="readOnlySpan"/> will be empty).</returns>
        /// <remarks>Note that the method can return true while assigning an empty <paramref name="readOnlySpan"/>,
        /// in case if the subkey exists, but its value is empty. Do not use the emptiness of the span
        /// as an indication of a successful search.</remarks>
        public bool TryGetValue(KeyRef key, ulong subkey, out ReadOnlySpan<byte> readOnlySpan)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new transaction based on the current snapshot.
        /// </summary>
        /// <returns>A new instance of a <see cref="Transaction"/>.</returns>
        public Transaction CreateTransaction()
        {
            throw new NotImplementedException();
        }

        internal StateSnapshot(ReplicatedState state, IntPtr stateHandle)
        {
            State = state;
            var info = new SnapshotInfo();
            handle = PInvoke_GetSnapshot(stateHandle, ref info);
            Version = info.Version;
            KeysCount = info.KeysCount.ToUInt64();
            SubkeysCount = info.SubkeysCount.ToUInt64();
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private ref struct SnapshotInfo
        {
            public ulong Version;
            public UIntPtr KeysCount;
            public UIntPtr SubkeysCount;
        }

        // Returns a handle to the obtained Snapshot.
        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Snapshot_Create")]
        private static extern IntPtr PInvoke_GetSnapshot(IntPtr storage_handle, ref SnapshotInfo snapshot_info);

        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
        "Microsoft_MixedReality_Sharing_StateSync_Snapshot_Contains")]
        private static extern bool PInvoke_Contains(IntPtr snapshot_handle, IntPtr key_handle, ulong subkey);

        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
        "Microsoft_MixedReality_Sharing_StateSync_Snapshot_GetSubkeysCount")]
        private static extern ulong PInvoke_GetSubkeysCount(IntPtr snapshot_handle, IntPtr key_handle);
    }
}
