// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface ITransaction
    {
        ISnapshot snapshot { get; }

        bool Set(IKey key, byte[] value);
        bool Set(byte[] key, byte[] value);

        bool Set(IKey key, object value);
        bool Set(byte[] key, object value);

        bool Delete(IKey key);
        bool Delete(byte[] key);
    }
}
