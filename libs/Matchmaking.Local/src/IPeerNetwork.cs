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
        /// <summary>
        /// Stream that the packet belongs to. See <see cref="IPeerNetwork.Broadcast(Guid, ArraySegment{byte})"/>.
        /// </summary>
        Guid StreamId { get; }

        /// <summary>
        /// Message payload.
        /// </summary>
        ArraySegment<byte> Contents { get; }
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
        /// <param name="streamId">
        /// Associates the message to a stream. Messages from the same stream will be delivered in order.
        /// No guarantees are made on messages from different stream.
        /// </param>
        /// <param name="message">The buffer containing the message to send</param>
        void Broadcast(Guid streamId, ArraySegment<byte> message);

        /// <summary>
        /// Reply to a message. (Typically a broadcast message)
        /// </summary>
        /// <param name="streamId">
        /// Associates the message to a stream. Messages from the same stream will be delivered in order.
        /// No guarantees are made on messages from different stream.
        /// </param>
        /// <param name="message">The buffer containing the message to send</param>
        void Reply(IPeerNetworkMessage inResponseTo, Guid streamId, ArraySegment<byte> message);
    }
}
