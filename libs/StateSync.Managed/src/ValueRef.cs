// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A lightweight unallocated ref struct that can be used to lookup values in the storage. 
    /// Can be converted to an allocated <see cref="Value"/>, <see cref="AsValue"/>.
    /// </summary>
    public readonly ref struct ValueRef
    {
        internal readonly IntPtr handle;

        public static implicit operator ValueRef(Value value)
        {
            return value.AsValueRef();
        }

        /// <summary>
        /// Constructs a new <see cref="Value"/> from this object.
        /// </summary>
        /// <returns><see cref="Value"/> that references the same internal representation.</returns>
        public Value AsValue()
        {
            Value.PInvoke_AddRef(handle);
            return new Value(handle);
        }

        /// <summary>
        /// Returns underlying bytes of the internal binary representation of the value.
        /// </summary>
        public ReadOnlySpan<byte> ToSpan()
        {
            return Value.ToSpan(handle);
        }

        internal ValueRef(IntPtr handle)
        {
            this.handle = handle;
        }
    }
}
