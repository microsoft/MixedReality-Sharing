using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.Utilities
{
    public static class StructExtensions
    {
        /// <summary>
        /// Converts a struct to a byte array using Marshalling.
        /// </summary>
        /// <typeparam name="T">The type of struct.</typeparam>
        /// <param name="this">The structure itself.</param>
        /// <returns>The <see cref="byte[]"/> containing the byte representation of the structure.</returns>
        public static byte[] ToBytes<T>(this T @this) where T : struct
        {
            byte[] arr = new byte[Marshal.SizeOf(@this)];
            GCHandle h = GCHandle.Alloc(arr, GCHandleType.Pinned);

            try
            {
                Marshal.StructureToPtr(@this, h.AddrOfPinnedObject(), false);

                return arr;
            }
            finally
            {
                h.Free();
            }
        }

        /// <summary>
        /// Conversts a struct to binary and copies it to a given byte array.
        /// </summary>
        /// <typeparam name="T">The type of struct.</typeparam>
        /// <param name="this">The structure itself.</param>
        /// <param name="buffer">The buffer to hold the data.</param>
        /// <param name="offset">The offset into the buffer at which to write the data.</param>
        /// <returns>Length of the data copied.</returns>
        public static int ToBytes<T>(this T @this, byte[] buffer, int offset = 0) where T : struct
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            int size = Marshal.SizeOf(@this);
            if (size < buffer.Length - offset)
            {
                throw new ArgumentException($"The buffer doesn't have enough capacity to write from '{offset}' position.", nameof(buffer));
            }

            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                Marshal.StructureToPtr(@this, IntPtr.Add(h.AddrOfPinnedObject(), offset), false);

                return size;
            }
            finally
            {
                h.Free();
            }
        }

        /// <summary>
        /// Converts the binary representation to a structure of type <see cref="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the struct.</typeparam>
        /// <param name="this">The binary representation.</param>
        /// <returns>The structure.</returns>
        public static T AsStruct<T>(this byte[] @this, int offset = 0) where T : struct
        {
            if (@this == null)
            {
                throw new ArgumentNullException(nameof(@this));
            }

            if (@this.Length - offset != Marshal.SizeOf<T>())
            {
                throw new ArgumentException("The binary data given is of incorrect length.", nameof(@this));
            }

            GCHandle h = GCHandle.Alloc(@this, GCHandleType.Pinned);

            try
            {
                return Marshal.PtrToStructure<T>(IntPtr.Add(h.AddrOfPinnedObject(), offset));
            }
            finally
            {
                h.Free();
            }
        }
    }
}
