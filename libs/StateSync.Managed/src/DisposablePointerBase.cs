// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Helper utility class for implementing IDispose when working with IntPtr's that need to be released.
    /// </summary>
    public abstract class DisposablePointerBase : IDisposable
    {
        internal IntPtr Pointer { get; private set; }

        protected DisposablePointerBase(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                throw new ArgumentException("The provided IntPtr was Zero, that is invalid.");
            }

            Pointer = pointer;
        }

        ~DisposablePointerBase()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                ReleasePointer(Pointer);
                Pointer = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Implement in the derived class to release the pointer on dispose.
        /// </summary>
        /// <param name="pointer">The pointer that should be released.</param>
        protected abstract void ReleasePointer(IntPtr pointer);

        /// <summary>
        /// If the object is disposed, throws an exception.
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (Pointer == IntPtr.Zero)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
