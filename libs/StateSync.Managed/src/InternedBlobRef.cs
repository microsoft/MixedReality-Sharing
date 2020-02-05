// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight stack-allocated reference to an interned blob.
    /// Can be converted to a heap-allocated <see cref="InternedBlob"/>, <see cref="AsInternedBlob"/>.
    /// </summary>
    public readonly ref struct InternedBlobRef
    {
        internal readonly IntPtr handle;

        public static implicit operator InternedBlobRef(InternedBlob blob)
        {
            return blob.AsBlobRef();
        }

        /// <summary>
        /// Constructs a new <see cref="InternedBlob"/> from this object.
        /// </summary>
        /// <returns><see cref="InternedBlob"/> that references the same internal representation.</returns>
        public InternedBlob AsInternedBlob()
        {
            InternedBlob.PInvoke.AddRef(handle);
            return new InternedBlob(handle);
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
        /// Indicates whether the current object is equal to the provided <see cref="InternedBlob"/>
        /// (referencing the same internal object).
        /// </summary>
        /// <param name="other">A <see cref="InternedBlob"/> object to compare with this object.</param>
        /// <returns>true if the current object references the same interned data; otherwise, false.</returns>
        public bool Equals(InternedBlob other)
        {
            return other.Equals(this);
        }

        /// <summary>
        /// Indicates whether the current object references the same interned data as the provided <see cref="InternedBlobRef"/>.
        /// </summary>
        /// <param name="other">A <see cref="InternedBlobRef"/> object to compare with this object.</param>
        /// <returns>true if the current object is equal to the other parameter (references the same interned data);
        /// otherwise, false.</returns>
        public bool Equals(InternedBlobRef other)
        {
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
            return InternedBlob.ToString(handle);
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
            return InternedBlob.ToSpan(handle);
        }

        /// <summary>
        /// A 64-bit hash of the blob.
        /// </summary>
        /// <remarks>
        /// The hash returned by GetHashCode() is obtained from this one by casting it to int.
        /// </remarks>
        public ulong Hash => InternedBlob.PInvoke.hash(handle);

        public override int GetHashCode()
        {
            return (int)InternedBlob.PInvoke.hash(handle);
        }

        internal InternedBlobRef(IntPtr handle)
        {
            this.handle = handle;
        }
    }
}
