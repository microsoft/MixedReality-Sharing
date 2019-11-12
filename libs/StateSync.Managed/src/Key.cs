// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An allocated key object for working with <see cref="ReplicatedState"/>
    /// (snapshots, transactions, etc.) The internal payload is always interned.
    /// </summary>
    /// <remarks>
    /// It is advisable to manually dispose of no longer required Key
    /// objects without waiting for the garbage collector.
    /// 
    /// See <see cref="KeyRef"/> for a lightweight ref struct variation of the same concept.
    /// </remarks>
    public class Key : Utilities.HandleOwner, IEquatable<Key>
    {
        /// <summary>
        /// Constructs the <see cref="Key"/> from the provided <see cref="string"/> content.
        /// </summary>
        /// <param name="content">A string that will be converted to UTF-8 and used as a payload of the key.</param>
        public unsafe Key(string content)
        {
            int bytesCount = System.Text.Encoding.UTF8.GetByteCount(content);
            int length = content.Length;
            ReadOnlySpan<byte> span = stackalloc byte[bytesCount];
            fixed (char* chars = content)
            fixed (byte* bytes = span)
            {
                System.Text.Encoding.UTF8.GetBytes(chars, length, bytes, bytesCount);
                handle = PInvoke_Create(bytes, bytesCount);
            }
        }

        /// <summary>
        /// Constructs the <see cref="Key"/> from the provided binary content.
        /// </summary>
        /// <param name="content">The binary content. It will be either copied
        /// or interned on the C++ side, so the span doesn't have to stay valid after the call.</param>
        public unsafe Key(ReadOnlySpan<byte> content)
        {
            fixed (byte* bytes = content)
            {
                handle = PInvoke_Create(bytes, content.Length);
            }
        }

        public KeyRef AsKeyRef()
        {
            return new KeyRef(handle);
        }

        /// <summary>
        /// Indicates whether two object instances are equal.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the provided parameter is a <see cref="Key"/>,
        /// and it references the same key; otherwise, false.</returns>
        public override bool Equals(object other)
        {
            return other is Key key && Equals(key);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another <see cref="Key"/>.
        /// </summary>
        /// <param name="other">A <see cref="Key"/> object to compare with this object.</param>
        /// <returns>true if the current object references the same key; otherwise, false.</returns>
        public bool Equals(Key other)
        {
            // Keys are interned, so comparing handles is safe.
            return handle == other.handle;
        }

        /// <summary>
        /// Indicates whether the current object references the same key as the provided <see cref="KeyRef"/>.
        /// </summary>
        /// <param name="other">A <see cref="KeyRef"/> object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter (references the same key);
        /// otherwise, false.</returns>
        public bool Equals(KeyRef other)
        {
            return handle == other.handle;
        }

        /// <summary>
        /// Attempts to convert this key to a <see cref="string"/>,
        /// expecting that the internal representation is a valid UTF-8 string.
        /// </summary>
        /// <remarks>
        /// Not all keys are convertible to strings, because they are allowed to be arbitrary binary blobs.
        /// Even if the key is representable as a <see cref="string"/>, it's not guaranteed to round trip
        /// to the same string used to construct it.
        /// </remarks>
        public override string ToString()
        {
            return ToString(handle);
        }

        /// <summary>
        /// Returns underlying bytes of the internal binary representation of the key.
        /// </summary>
        /// <remarks>
        /// Keys constructed from bytes will always contain return the same bytes,
        /// regardless of the content.
        /// Keys constructed from strings will return the binary representation
        /// of the string after it was converted to UTF-8.
        /// </remarks>
        public ReadOnlySpan<byte> ToSpan()
        {
            return ToSpan(handle);
        }

        /// <summary>
        /// A 64-bit hash of the key.
        /// </summary>
        /// <remarks>
        /// The hash returned by GetHashCode() is obtained from this one by casting it to int.
        /// </remarks>
        public ulong Hash => PInvoke_hash(handle);

        public override int GetHashCode()
        {
            return (int)PInvoke_hash(handle);
        }

        internal Key(IntPtr handle)
        {
            this.handle = handle;
        }

        protected override bool ReleaseHandle()
        {
            PInvoke_RemoveRef(handle);
            return true;
        }

        internal static unsafe string ToString(IntPtr handle)
        {
            int size = 0;
            byte* bytes = PInvoke_view(handle, ref size);
            return System.Text.Encoding.UTF8.GetString(bytes, size);
        }

        internal static unsafe ReadOnlySpan<byte> ToSpan(IntPtr handle)
        {
            int size = 0;
            byte* bytes = PInvoke_view(handle, ref size);
            return new ReadOnlySpan<byte>(bytes, size);
        }

        // Returns a key handle.
        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Key_Create")]
        private static extern unsafe IntPtr PInvoke_Create(byte* data_ptr, int size);

        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Key_AddRef")]
        internal static extern void PInvoke_AddRef(IntPtr handle);

        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Key_RemoveRef")]
        private static extern void PInvoke_RemoveRef(IntPtr handle);
               
        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Key_hash")]
        internal static extern ulong PInvoke_hash(IntPtr handle);

        // Returns the pointer to the beginning of the view
        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_Key_view")]
        private static extern unsafe byte* PInvoke_view(IntPtr handle, ref int out_size);
    }
}
