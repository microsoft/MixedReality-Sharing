// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync.Utilities
{
    /// <summary>
    /// The base class for C# wrappers around C++ implementation objects.
    /// Ensures that the implementation object is correctly released
    /// (either in the finalizer, or when the object is disposed).
    /// </summary>
    public abstract class HandleOwner : CriticalHandle
    {
        protected HandleOwner() : base(IntPtr.Zero) {}

        public override bool IsInvalid => handle != IntPtr.Zero;
    }

    /// <summary>
    /// The base class for C# wrappers around objects inheriting from VirtualRefCountedBase on C++ side.
    /// </summary>
    public class VirtualRefCountedBase : HandleOwner
    {
        protected override bool ReleaseHandle()
        {
            VirtualRefCountedBase_RemoveRef(handle);
            return true;
        }

        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_VirtualRefCountedBase_RemoveRef")]
        public static extern void VirtualRefCountedBase_RemoveRef(IntPtr handle);
    }
}
