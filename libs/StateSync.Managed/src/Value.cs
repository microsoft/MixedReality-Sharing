// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An allocated reference counted value that can be stored in a <see cref="ReplicatedState"/>.
    /// </summary>
    /// <remarks>
    /// It is advisable to manually dispose of no longer required Value
    /// objects without waiting for the garbage collector.
    /// 
    /// See <see cref="ValueRef"/> for a lightweight ref struct variation of the same concept.
    /// </remarks>
    public class Value : Utilities.HandleOwner
    {
        /// <summary>
        /// Constructs the <see cref="Value"/> from the provided <see cref="string"/> content.
        /// </summary>
        /// <param name="content">The binary content. It will be copied internally on the C++ side,
        /// so the span doesn't have to stay valid after the call</param>
        public unsafe Value(ReadOnlySpan<byte> content)
        {
            fixed (byte* bytes = content)
            {
                handle = PInvoke_Create(bytes, content.Length);
            }
        }

        public ValueRef AsValueRef()
        {
            return new ValueRef(handle);
        }

        /// <summary>
        /// Returns underlying bytes of the internal binary representation of the value.
        /// </summary>
        public ReadOnlySpan<byte> ToSpan()
        {
            return ToSpan(handle);
        }

        protected override bool ReleaseHandle()
        {
            PInvoke_RemoveRef(handle);
            return true;
        }

        internal Value(IntPtr handle)
        {
            this.handle = handle;
        }

        internal static unsafe ReadOnlySpan<byte> ToSpan(IntPtr handle)
        {
            int size = 0;
            byte* bytes = PInvoke_view(handle, ref size);
            return new ReadOnlySpan<byte>(bytes, size);
        }

        // Returns a value handle.
        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Value_Create")]
        private static extern unsafe IntPtr PInvoke_Create(byte* data_ptr, int size);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Value_AddRef")]
        internal static extern void PInvoke_AddRef(IntPtr handle);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Value_RemoveRef")]
        private static extern void PInvoke_RemoveRef(IntPtr handle);

        // Returns the pointer to the beginning of the view
        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Value_view")]
        private static extern unsafe byte* PInvoke_view(IntPtr handle, ref int out_size);
    }
}
