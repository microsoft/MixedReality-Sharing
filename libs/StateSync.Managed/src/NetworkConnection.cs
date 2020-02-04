// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface NetworkConnection
    {
        void SendMessage(ReadOnlySpan<byte> message);
    }
}
