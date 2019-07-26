// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Simple implementation of <see cref="MessageQueue"/>.
    /// </summary>
    public class MessageQueue : IMessageQueue
    {
        private BlockingCollection<Message> queue_ = new BlockingCollection<Message>();

        public Message Dequeue(CancellationToken token)
        {
            return queue_.Take(token);
        }

        public Message[] DequeueAll(CancellationToken token)
        {
            // Emulate the expected behavior.

            // Block until at least one message is in the queue.
            var res = new List<Message>();
            res.Add(queue_.Take(token));

            // Take more messages if available.
            Message[] more;
            if (TryDequeueAll(out more))
            {
                res.AddRange(more);
            }
            return res.ToArray();
        }

        public bool TryDequeue(out Message message)
        {
            return queue_.TryTake(out message);
        }

        public bool TryDequeueAll(out Message[] messages)
        {
            // Emulate the expected behavior.
            messages = null;

            int currentCount = queue_.Count;
            if (currentCount == 0)
            {
                return false;
            }

            // Take the current messages in the queue one by one.
            var res = new List<Message>(currentCount);
            Message msg;
            // Also check if there are still messages in case some other thread is taking from the same queue.
            while(res.Count < currentCount && queue_.TryTake(out msg))
            {
                res.Add(msg);
            }
            if (res.Count == 0)
            {
                return false;
            }
            messages = res.ToArray();
            return true;
        }

        public void Add(Message message)
        {
            queue_.Add(message);
        }
    }
}
