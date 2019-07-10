// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public struct SubkeyValue
    {
        object value;
        ulong subkey;
    }

    public interface ISnapshot : IDisposable
    {
        int Version { get; }
        ulong KeysCount { get; }
        ulong SubkeysCount { get; }

        bool Get(IKey key, ulong subkey, out byte[] value);
        bool Get(IKey key, ulong subkey, out object value);

        ulong GetSubkeysCount(IKey key);

        bool Contains(IKey key, ulong subkey);

        IEnumerable<IKey> CreateKeyEnumerator();
        IEnumerable<SubkeyValue> CreateSubkeyEnumerator(IKey key, ulong min_key=0);
    }
}
