// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight unallocated key that can be used to lookup values in the storage. 
    /// This key can be converted to an allocated <see cref="Key"/>, <see cref="AsKey"/>.
    /// </summary>
    public readonly ref struct KeyRef
    {
        internal readonly IntPtr handle;

        public static implicit operator KeyRef(Key key)
        {
            return key.AsKeyRef();
        }

        /// <summary>
        /// Constructs a new <see cref="Key"/> from this object.
        /// </summary>
        /// <returns><see cref="Key"/> that references the same internal representation.</returns>
        public Key AsKey()
        {
            Key.PInvoke_AddRef(handle);
            return new Key(handle);
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
        /// Indicates whether the current object is equal to the provided <see cref="Key"/>
        /// (referencing the same internal object).
        /// </summary>
        /// <param name="other">A <see cref="Key"/> object to compare with this object.</param>
        /// <returns>true if the current object references the same key; otherwise, false.</returns>
        public bool Equals(Key other)
        {
            return other.Equals(this);
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
            return Key.ToString(handle);
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
            return Key.ToSpan(handle);
        }

        /// <summary>
        /// A 64-bit hash of the key.
        /// </summary>
        /// <remarks>
        /// The hash returned by GetHashCode() is obtained from this one by casting it to int.
        /// </remarks>
        public ulong Hash => Key.PInvoke_hash(handle);

        public override int GetHashCode()
        {
            return (int)Key.PInvoke_hash(handle);
        }

        internal KeyRef(IntPtr handle)
        {
            this.handle = handle;
        }
    }
}
