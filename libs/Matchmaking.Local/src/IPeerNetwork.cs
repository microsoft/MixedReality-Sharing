// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface IPeerNetworkMessage
    {
        byte[] Message { get; }
    }

    public interface IPeerNetwork
    {
        event Action<IPeerNetwork, IPeerNetworkMessage> Message;

        void Start();
        void Stop();

        void Broadcast(byte[] msg);
        void Reply(IPeerNetworkMessage inResponseTo, byte[] message);
    }
}
