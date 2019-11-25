// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public class PInvokeAPI
    {
        public const string LibraryName =
            "../x64/Microsoft.MixedReality.Sharing.StateSync-pinvoke-cpp.dll";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct KeyHandleWrapper
        {
            public ulong key_handle;  // Note: not IntPtr.
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct SubkeyIteratorState
        {
            public ulong version;
            public unsafe void* currentStateBlock;
            public unsafe void* indexBegin;
            public unsafe void* dataBegin;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public ref struct RawSubkeyView
        {
            public ulong subkey;
            public ulong version;
            public unsafe byte* data;
            public UIntPtr size;  // FIXME: unify the size convention for PInvoke
            public int IntSize { get { checked { return (int)size; } } }
        }

        [DllImport(LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_SubkeyIteratorState_Init")]
        public static extern unsafe void SubkeyIteratorState_Init(
            void* key_handle_wrapper, IntPtr snapshot_handle, ref SubkeyIteratorState subkey_iterator_state, ref RawSubkeyView subkey_view);

        [DllImport(LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_SubkeyIteratorState_Advance")]
        public static extern unsafe void SubkeyIteratorState_Advance(
            ref SubkeyIteratorState subkey_iterator_state, ref RawSubkeyView subkey_view);
    }
}
