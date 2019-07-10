﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface IMessage
    {
        IEndpoint Sender { get; }
        IChannelCategory Category { get; }
        byte[] Payload { get; }
    }

    /// <summary>
    /// Blocking queue used to store received messages waiting to be processed.
    /// </summary>
    public interface IMessageQueue
    {
        /// <summary>
        /// Block until a message is available, then remove the message from the queue and return it.
        /// </summary>
        IMessage Dequeue();

        /// <summary>
        /// Remove a message from the queue and return it in <paramref name="message"/> if there is one.
        /// </summary>
        /// <returns>`true` if a message was available, `false` otherwise.</returns>
        bool TryDequeue(out IMessage message);

        /// <summary>
        /// Block until at least one message is available, then remove the messages from the queue and return them.
        /// </summary>
        IMessage[] DequeueAll();

        /// <summary>
        /// Remove all messages from the queue and return them in <paramref name="messages"/> if there are any.
        /// </summary>
        /// <returns>`true` if at least one message was available, `false` otherwise.</returns>
        bool TryDequeueAll(out IMessage[] messages);

        // TODO maybe we want to have adding controlled by the network impl and expose a queue factory instead?
        void Add(IMessage message);
    }
}
