// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Interface for IPeerNetwork messages.
    /// </summary>
    /// <remarks>
    /// Only implementations of IPeerNetwork should implement this interface.
    /// </remarks>
    public interface IPeerNetworkMessage
    {
        byte[] Message { get; }
    }

    /// <summary>
    /// Transport layer abstraction for PeerMatchmakingService.
    /// Implement this interface to use peer matchmaking over a different transport layer.
    /// </summary>
    public interface IPeerNetwork
    {
        /// <summary>
        /// Raised when a message arrives on this network.
        /// </summary>
        event Action<IPeerNetwork, IPeerNetworkMessage> Message;

        /// <summary>
        /// Start the network.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the network
        /// </summary>
        void Stop();

        /// <summary>
        /// Send a message to all others in this network.
        /// </summary>
        void Broadcast(byte[] message);

        /// <summary>
        /// Reply to a message. (Typically a broadcast message)
        /// </summary>
        void Reply(IPeerNetworkMessage inResponseTo, byte[] message);
    }
}
