// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight stack-allocated reference to an interned blob.
    /// Can be converted to a heap-allocated <see cref="Blob"/>, <see cref="AsBlob"/>.
    /// </summary>
    public readonly ref struct BlobRef
    {
        internal readonly IntPtr handle;

        public static implicit operator BlobRef(Blob blob)
        {
            return blob.AsBlobRef();
        }

        /// <summary>
        /// Constructs a new <see cref="Blob"/> from this object.
        /// </summary>
        /// <returns><see cref="Blob"/> that references the same internal representation.</returns>
        public Blob AsBlob()
        {
            Blob.PInvoke.AddRef(handle);
            return new Blob(handle);
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
            return Blob.ToSpan(handle);
        }

        internal BlobRef(IntPtr handle)
        {
            this.handle = handle;
        }
    }
}
