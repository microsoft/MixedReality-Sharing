// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Simple implementation of <see cref="IMessageQueue"/>.
    /// </summary>
    public class MessageQueue : IMessageQueue
    {
        private BlockingCollection<IMessage> queue_ = new BlockingCollection<IMessage>();

        public IMessage Dequeue()
        {
            return queue_.Take();
        }

        public IMessage[] DequeueAll()
        {
            // Emulate the expected behavior.

            // Block until at least one message is in the queue.
            var res = new List<IMessage>();
            res.Add(queue_.Take());

            // Take more messages if available.
            IMessage[] more;
            if (TryDequeueAll(out more))
            {
                res.AddRange(more);
            }
            return res.ToArray();
        }

        public bool TryDequeue(out IMessage message)
        {
            return queue_.TryTake(out message);
        }

        public bool TryDequeueAll(out IMessage[] messages)
        {
            // Emulate the expected behavior.
            // Take a bunch of messages from the queue one by one.
            var res = new List<IMessage>();
            IMessage msg;
            while(queue_.TryTake(out msg) && res.Count < 256)
            {
                res.Add(msg);
            }
            if (res.Count == 0)
            {
                messages = null;
                return false;
            }
            messages = res.ToArray();
            return true;
        }

        public void Add(IMessage message)
        {
            queue_.Add(message);
        }
    }
}
