// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;

namespace Microsoft.MixedReality.Sharing.Network.Socketer
{
    internal class SocketerChannel : IChannel
    {
        private SocketerChannelCategoryFactory factory_;
        private BlockingCollection<byte[]> queue_;

        public SocketerChannel(SocketerChannelCategory category, SocketerEndpoint endpoint, SocketerChannelCategoryFactory factory)
        {
            Category = category;
            Endpoint = endpoint;
            factory_ = factory;

            // todo: in theory this could simply make a queue and offload the rest of the work
            queue_ = factory.CreateChannelQueue(endpoint, category.Type);
        }

        public SocketerChannelCategory Category { get; }

        public SocketerEndpoint Endpoint { get; }

        public bool IsOk => (queue_ != null); // TODO

        // TODO
        public int SendQueueCount => queue_.Count;

        IChannelCategory IChannel.Category => Category;

        IEndpoint IChannel.Endpoint => Endpoint;

        public void Dispose()
        {
            factory_.DisposeChannelQueue(queue_, Endpoint, Category.Type);
            queue_ = null;
        }

        public void Reconnect()
        {
            factory_.DisposeChannelQueue(queue_, Endpoint, Category.Type);
            queue_ = factory_.CreateChannelQueue(Endpoint, Category.Type);
        }

        public void SendMessage(byte[] message)
        {
            var toSend = Utils.PrependCategory(message, Category.Header);
            if (toSend.Length > 65507 && Category.Type == ChannelType.Unordered)
            {
                throw new ArgumentException("Message does not fit in UDP datagram (size: " + toSend.Length + ")");
            }
            queue_.Add(toSend);
        }
    }
}
