// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An immutable interned blob of bytes.
    /// </summary>
    /// <remarks>
    /// It is advisable to manually dispose of no longer required blobs
    /// without waiting for the garbage collector.
    /// 
    /// See <see cref="InternedBlobRef"/> for a lightweight ref struct variation of the same concept.
    /// </remarks>
    public class InternedBlob : Utilities.HandleOwner, IEquatable<InternedBlob>
    {
        /// <summary>
        /// Constructs the <see cref="InternedBlob"/> from the provided <see cref="string"/> content.
        /// </summary>
        /// <param name="content">A string that will be converted to UTF-8 and stored as bytes.</param>
        public unsafe InternedBlob(string content)
        {
            int bytesCount = System.Text.Encoding.UTF8.GetByteCount(content);
            int length = content.Length;
            ReadOnlySpan<byte> span = stackalloc byte[bytesCount];
            fixed (char* chars = content)
            fixed (byte* bytes = span)
            {
                System.Text.Encoding.UTF8.GetBytes(chars, length, bytes, bytesCount);
                handle = PInvoke.Create(bytes, bytesCount);
            }
        }

        /// <summary>
        /// Constructs the <see cref="InternedBlob"/> from the provided binary content.
        /// </summary>
        /// <param name="content">The binary content. It will be either copied
        /// or interned on the C++ side, so the span doesn't have to stay valid after the call.</param>
        public unsafe InternedBlob(ReadOnlySpan<byte> content)
        {
            fixed (byte* bytes = content)
            {
                handle = PInvoke.Create(bytes, content.Length);
            }
        }

        public InternedBlobRef AsBlobRef()
        {
            return new InternedBlobRef(handle);
        }

        /// <summary>
        /// Indicates whether two object instances are equal.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the provided parameter is a <see cref="InternedBlob"/>,
        /// and it references the same interned data; otherwise, false.</returns>
        public override bool Equals(object other)
        {
            return other is InternedBlob blob && Equals(blob);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another <see cref="InternedBlob"/>.
        /// </summary>
        /// <param name="other">A <see cref="InternedBlob"/> object to compare with this object.</param>
        /// <returns>true if the current object references the same interned data; otherwise, false.</returns>
        public bool Equals(InternedBlob other)
        {
            // Blobs are interned, so just comparing handles is enough.
            return handle == other.handle;
        }

        /// <summary>
        /// Indicates whether the current object references the same internal data as the provided <see cref="InternedBlobRef"/>.
        /// </summary>
        /// <param name="other">A <see cref="InternedBlobRef"/> object to compare with this object.</param>
        /// <returns>true if the current object references the same interned data; otherwise, false.</returns>
        public bool Equals(InternedBlobRef other)
        {
            // Blobs are interned, so just comparing handles is enough.
            return handle == other.handle;
        }

        /// <summary>
        /// Attempts to convert this blob to a <see cref="string"/>,
        /// expecting that the internal representation is a valid UTF-8 string.
        /// </summary>
        /// <remarks>
        /// Not all blobs are convertible to strings.
        /// Even if the blob is representable as a <see cref="string"/>, it's not guaranteed to round trip
        /// to the same string used to construct it.
        /// </remarks>
        public override string ToString()
        {
            return ToString(handle);
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

        /// <summary>
        /// A 64-bit hash of the blob.
        /// </summary>
        /// <remarks>
        /// The hash returned by GetHashCode() is obtained from this one by casting it to int.
        /// </remarks>
        public ulong Hash => PInvoke.hash(handle);

        public override int GetHashCode()
        {
            return (int)PInvoke.hash(handle);
        }

        internal InternedBlob(IntPtr handle)
        {
            this.handle = handle;
        }

        protected override bool ReleaseHandle()
        {
            PInvoke.RemoveRef(handle);
            return true;
        }

        internal static unsafe string ToString(IntPtr handle)
        {
            int size = 0;
            byte* bytes = PInvoke.view(handle, ref size);
            return System.Text.Encoding.UTF8.GetString(bytes, size);
        }

        internal static unsafe ReadOnlySpan<byte> ToSpan(IntPtr handle)
        {
            int size = 0;
            byte* bytes = PInvoke.view(handle, ref size);
            return new ReadOnlySpan<byte>(bytes, size);
        }

        internal static class PInvoke
        {
            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_InternedBlob_Create")]
            internal static extern unsafe IntPtr Create(byte* data_ptr, int size);

            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_InternedBlob_AddRef")]
            internal static extern void AddRef(IntPtr handle);

            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_InternedBlob_RemoveRef")]
            internal static extern void RemoveRef(IntPtr handle);

            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_InternedBlob_hash")]
            internal static extern ulong hash(IntPtr handle);

            // Returns the pointer to the beginning of the view
            [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
                "Microsoft_MixedReality_Sharing_InternedBlob_view")]
            internal static extern unsafe byte* view(IntPtr handle, ref int out_size);
        }
    }
}
