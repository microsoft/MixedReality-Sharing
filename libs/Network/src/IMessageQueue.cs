using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Network
{
    public class Message
    {
        public IEndpoint Sender { get; }
        public IChannelCategory Category { get; }
        public byte[] Payload { get; }

        public Message(IEndpoint sender, IChannelCategory category, byte[] payload)
        {
            Sender = sender;
            Category = category;
            Payload = payload;
        }
    }

    /// <summary>
    /// Blocking queue used to store received messages waiting to be processed.
    /// </summary>
    public interface IMessageQueue
    {
        /// <summary>
        /// Block until a message is available, then remove the message from the queue and return it.
        /// </summary>
        Message Dequeue(CancellationToken token = default);

        /// <summary>
        /// Remove a message from the queue and return it in <paramref name="message"/> if there is one.
        /// </summary>
        /// <returns>`true` if a message was available, `false` otherwise.</returns>
        bool TryDequeue(out Message message);

        /// <summary>
        /// Block until at least one message is available, then remove the messages from the queue and return them.
        /// </summary>
        Message[] DequeueAll(CancellationToken token = default);

        /// <summary>
        /// Remove all messages from the queue and return them in <paramref name="messages"/> if there are any.
        /// </summary>
        /// <returns>`true` if at least one message was available, `false` otherwise.</returns>
        bool TryDequeueAll(out Message[] messages);
    }
}
