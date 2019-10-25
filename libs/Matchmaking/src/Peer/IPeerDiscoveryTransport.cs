// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Interface for IPeerDiscoveryTransport messages.
    /// </summary>
    /// <remarks>
    /// Only implementations of IPeerDiscoveryTransport should implement this interface.
    /// </remarks>
    public interface IPeerDiscoveryMessage
    {
        /// <summary>
        /// Stream that the packet belongs to. See <see cref="IPeerDiscoveryTransport.Broadcast(Guid, ArraySegment{byte})"/>.
        /// </summary>
        Guid StreamId { get; }

        /// <summary>
        /// Message payload.
        /// </summary>
        ArraySegment<byte> Contents { get; }
    }

    /// <summary>
    /// Transport layer abstraction for PeerDiscoveryAgent.
    /// Implement this interface to use peer discovery over a different transport layer.
    /// </summary>
    public interface IPeerDiscoveryTransport
    {
        /// <summary>
        /// Raised when a message arrives on this transport.
        /// </summary>
        event Action<IPeerDiscoveryTransport, IPeerDiscoveryMessage> Message;

        /// <summary>
        /// Start the transport.
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the transport
        /// </summary>
        void Stop();

        /// <summary>
        /// Send a message to all others in this transport.
        /// </summary>
        /// <param name="streamId">
        /// Associates the message to a stream. Messages from the same stream will be delivered in order.
        /// No guarantees are made on messages from different stream. <see cref="Guid.Empty"/> can be used
        /// for messages that do not need ordering.
        /// </param>
        /// <param name="message">The buffer containing the message to send</param>
        void Broadcast(Guid streamId, ArraySegment<byte> message);

        /// <summary>
        /// Reply to a message. (Typically a broadcast message)
        /// </summary>
        /// <param name="streamId">
        /// Associates the message to a stream. Messages from the same stream will be delivered in order.
        /// No guarantees are made on messages from different stream. <see cref="Guid.Empty"/> can be used
        /// for messages that do not need ordering.
        /// </param>
        /// <param name="message">The buffer containing the message to send</param>
        void Reply(IPeerDiscoveryMessage inResponseTo, Guid streamId, ArraySegment<byte> message);
    }
}
