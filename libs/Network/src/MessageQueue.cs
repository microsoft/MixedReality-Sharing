﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
            // Take a bunch of messages from the queue one by one.
            var res = new List<Message>();
            Message msg;
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

        public void Add(Message message)
        {
            queue_.Add(message);
        }
    }
}
