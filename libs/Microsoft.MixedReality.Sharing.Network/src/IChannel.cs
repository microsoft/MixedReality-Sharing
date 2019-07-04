// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Network
{
    public enum ChannelType
    {
        /// <summary>
        /// Guarantees that every message is either eventually delivered in its entirety, or dropped (no
        /// fragmented/corrupted messages will be received).
        /// </summary>
        Unordered,

        /// <summary>
        /// In addition to the `Unordered` guarantees, this guarantees that:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// each message is retransmitted until its delivery is confirmed, or the connection breaks;
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// any sequence of messages is eventually delivered in the same order as it was sent, or dropped (no
        /// out-of-order messages or duplicates will be received).
        /// </description>
        /// </item>
        /// </list>
        /// </summary>
        Ordered
    }

    public class ChannelConfig
    {
        public string Name;
        public ChannelType Type;
    }

    public interface IChannel : IDisposable
    {
        ChannelConfig Config { get; }

        IEndpoint Endpoint { get; }

        void SendMessage(byte[] message);

        event EventHandler<byte[]> MessageReceived;

        /// <summary>
        /// Tells whether the channel is working. If this returns false, the channel shouldn't be used to send/receive
        /// messages.
        /// Unordered channels will typically return false only when some issue is sure to prevent any
        /// communication with the endpoint (e.g. no network adapter). Ordered channels will return false when there is
        /// some possibility that messages have been lost.
        /// If this returns false, <see cref="Reconnect"/> can be called to try to restore the channel.
        /// </summary>
        bool IsOk { get; }

        /// <summary>
        /// Try to re-establish the channel. If <see cref="IsOk"/> returns false, this might restore the channel
        /// status and make it available for sending/receiving again.
        /// </summary>
        void Reconnect();

        /// <summary>
        /// Number of messages currently queued to be sent. Can be used for throttling.
        /// </summary>
        int SendQueueCount { get; }
    }
}
