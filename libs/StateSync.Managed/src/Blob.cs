// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An heap-allocated reference counted blob that can be stored in a <see cref="ReplicatedState"/>.
    /// </summary>
    /// <remarks>
    /// It is advisable to manually dispose of no longer required Blob
    /// objects without waiting for the garbage collector.
    /// 
    /// See <see cref="BlobRef"/> for a lightweight ref struct variation of the same concept.
    /// </remarks>
    public class Blob : Utilities.HandleOwner
    {
        /// <summary>
        /// Constructs the <see cref="Blob"/> from the provided <see cref="string"/> content.
        /// </summary>
        /// <param name="content">The binary content. It will be copied internally on the C++ side,
        /// so the span doesn't have to stay valid after the call</param>
        public unsafe Blob(ReadOnlySpan<byte> content)
        {
            fixed (byte* bytes = content)
            {
                handle = PInvoke.Create(bytes, content.Length);
            }
        }

        public BlobRef AsBlobRef()
        {
            return new BlobRef(handle);
        }

        /// <summary>
        /// Returns the content of the blob.
        /// </summary>
        /// <remarks>
        /// Blobs constructed from bytes will always contain return the same bytes,
        /// regardless of the content.
        /// Blobs constructed from strings will return the binary representation
        /// of the string after it was converted to UTF-8.
        /// </remarks>
        public ReadOnlySpan<byte> ToSpan()
        {
            return ToSpan(handle);
        }

        protected override bool ReleaseHandle()
        {
            PInvoke.RemoveRef(handle);
            return true;
        }

        internal Blob(IntPtr handle)
        {
            this.handle = handle;
        }

        internal static unsafe ReadOnlySpan<byte> ToSpan(IntPtr handle)
        {
            int size = 0;
            byte* bytes = PInvoke.view(handle, ref size);
            return new ReadOnlySpan<byte>(bytes, size);
        }

        internal static class PInvoke
        {
            // Returns a blob handle.
            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_StateSync_Blob_Create")]
            internal static extern unsafe IntPtr Create(byte* data_ptr, int size);

            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_StateSync_Blob_AddRef")]
            internal static extern void AddRef(IntPtr handle);

            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_StateSync_Blob_RemoveRef")]
            internal static extern void RemoveRef(IntPtr handle);

            // Returns the pointer to the beginning of the view
            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_StateSync_Blob_view")]
            internal static extern unsafe byte* view(IntPtr handle, ref int out_size);
        }
    }
}
