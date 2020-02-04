// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight structure that represents the state of a subkey in a <see cref="StateSnapshot"/>.
    /// </summary>
    public readonly ref struct SubkeySnapshot
    {
        /// <summary>
        /// Observed snapshot.
        /// </summary>
        public readonly StateSnapshot Snapshot;

        /// <summary>
        /// Observed subkey.
        /// </summary>
        public readonly ulong Subkey;

        /// <summary>
        /// The version of the storage at which the observed value was assigned,
        /// or an invalid version if the subkey doesn't exist in the observed state.
        /// </summary>
        /// <remarks>
        /// When observing a snapshot, this field is never greater than the
        /// version of the snapshot. However, it can be less (if the current
        /// value was assigned a few versions ago).
        /// 
        /// If two versions of the same subkey are the same for two different
        /// snapshots of the same <see cref="ReplicatedState"/>, the values
        /// are guaranteed to be the same.
        /// 
        /// If two versions of the same subkey are different by 1, the values
        /// are also guaranteed to be different.
        /// (TODO: validate that we actually guarantee this property.)
        /// 
        /// If two versions of the same subkey are different by more than 1,
        /// it indicates that there were some updates to the subkey, but the
        /// newer value can be either the same as the older one or different.
        /// </remarks>
        public readonly ulong Version;

        /// <summary>
        /// Indicates whether the subkey has a defined value.
        /// </summary>
        /// <remarks>Note that there is a difference between having no value
        /// and having an empty value.</remarks>
        public bool HasValue => Constants.IsVersionValid(Version);

        /// <summary>
        /// The span representing the value associated with this subkey.
        /// </summary>
        /// <remarks>Prefer this property to <see cref="GetValue"/> when the value doesn't need to be saved
        /// independently from any snapshots (it's faster than creating a Value object).
        /// 
        /// The observed memory is guaranteed to stay valid for as long as it is referenced by
        /// either a <see cref="Snapshot"/> or a <see cref="Value"/> object extracted from this
        /// snapshot, so if the code just works with a <see cref="SubkeySnapshot"/> on the stack,
        /// it's always better to use <see cref="ValueSpan"/>.</remarks>
        public ReadOnlySpan<byte> ValueSpan {
            get {
                if (!Constants.IsVersionValid(Version))
                    throw new InvalidOperationException("The subkey has no defined value");
                return _valueSpan;
            }
        }
        private readonly ReadOnlySpan<byte> _valueSpan;

        /// <summary>
        /// Returns the observed value or null if it doesn't exist.
        /// </summary>
        /// <remarks>Unlike <see cref="ValueSpan"/>, the returned <see cref="Value"/> doesn't require
        /// the snapshot to be alive. An extra reference will be added to the internally allocated
        /// object with the actual payload, and thus it's faster than saving the content of ValueSpan
        /// somewhere manually.
        /// If the ownership of the value is not required (for example, the code just wants to read the value
        /// once), it's faster to use <see cref="ValueSpan"/>.</remarks>
        public unsafe Value GetValue()
        {
            if (Constants.IsVersionValid(Version))
            {
                fixed (byte* bytes = _valueSpan)
                {
                    return new Value(PInvoke_GetValue(bytes));
                }
            }
            return null;
        }

        internal unsafe SubkeySnapshot(StateSnapshot snapshot, ulong subkey, ulong version, byte* spanBegin, int spanSize)
        {
            Snapshot = snapshot;
            Subkey = subkey;
            Version = version;
            _valueSpan = new ReadOnlySpan<byte>(spanBegin, spanSize);
        }

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_SubkeyView_GetValue")]
        private static extern unsafe IntPtr PInvoke_GetValue(void* data);
    }
}
