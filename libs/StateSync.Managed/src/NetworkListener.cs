// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface NetworkListener
    {
        void OnMessage(InternedBlobRef senderConnectionString, ReadOnlySpan<byte> message);
    }
}
