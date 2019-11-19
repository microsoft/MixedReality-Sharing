// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    class MemoryPeerDiscoveryMessage : IPeerDiscoveryMessage
    {
        internal MemoryPeerDiscoveryTransport sender_;
        public Guid StreamId { get; }
        public ArraySegment<byte> Contents { get; }

        internal MemoryPeerDiscoveryMessage(MemoryPeerDiscoveryTransport sender, Guid streamId, ArraySegment<byte> contents)
        {
            sender_ = sender;
            Contents = contents;
            StreamId = streamId;
        }
    }

    public class MemoryPeerDiscoveryTransport : IPeerDiscoveryTransport
    {
        int ident_;
        ConcurrentQueue<MemoryPeerDiscoveryMessage> incoming_ = new ConcurrentQueue<MemoryPeerDiscoveryMessage>();

        //TODO extract into a factory so we can have independent transports
        static volatile List<MemoryPeerDiscoveryTransport> instances_ = new List<MemoryPeerDiscoveryTransport>();

        const int MessagePumpInProgress = 1;
        const int MessageQueuedSome = 2;
        static int transportStatus_ = 0;

        public MemoryPeerDiscoveryTransport(int ident)
        {
            ident_ = ident;
        }

        public void Start()
        {
            // In Start() and Stop() we replace the entire list so that we don't invalidate
            // any existing iterators.
            lock (instances_)
            {
                if (!instances_.Contains(this))
                {
                    var i = new List<MemoryPeerDiscoveryTransport>(instances_);
                    i.Add(this);
                    instances_ = i;
                }
            }
        }

        public void Stop()
        {
            lock (instances_)
            {
                if (instances_.Contains(this))
                {
                    var i = new List<MemoryPeerDiscoveryTransport>(instances_);
                    i.Remove(this);
                    instances_ = i;
                }
            }
        }

        public event Action<IPeerDiscoveryTransport, IPeerDiscoveryMessage> Message;

        public void Broadcast(Guid streamId, ArraySegment<byte> message)
        {
            var m = new MemoryPeerDiscoveryMessage(this, streamId, message);
            foreach (var c in instances_)
            {
                c.incoming_.Enqueue(m);
            }
            PumpMessages();
        }

        public void Reply(IPeerDiscoveryMessage req, Guid streamId, ArraySegment<byte> message)
        {
            var r = req as MemoryPeerDiscoveryMessage;
            var m = new MemoryPeerDiscoveryMessage(this, streamId, message);
            r.sender_.incoming_.Enqueue(m);
            PumpMessages();
        }

        static protected void PumpMessagesInternal()
        {
            // Notation - transportStatus is two bits : P = MessagePumpInProgress, Q = MessageQueuedSome
            // The only valid states are [00, P0, PQ], [0Q] is invalid.
            while (true)
            {
                // We're about to process the whole list so clear the MessageQueuedSome bit. [P? -> P0]
                Interlocked.CompareExchange(ref transportStatus_, MessagePumpInProgress, MessagePumpInProgress | MessageQueuedSome);

                // Raise the message events
                foreach (var c in instances_)
                {
                    MemoryPeerDiscoveryMessage msg;
                    while (c.incoming_.TryDequeue(out msg))
                    {
                        try
                        {
                            c.Message?.Invoke(c, msg);
                        }
                        catch(Exception e)
                        {
                            Log.Error(e, "Exception raised while handling message");
                        }
                    }
                }

                // Exit if no more work has been queued while we were working. i.e. MessageQueuedSome is still clear.
                if (Interlocked.CompareExchange(ref transportStatus_, 0, MessagePumpInProgress) == MessagePumpInProgress)
                {
                    return; // [P0 -> 00]
                }
            }
        }

        // Called after a Broadcast() or Reply() to ensure the message is delivered.
        // The first thread to send a message becomes the pumper until all messages are delivered.
        static protected void PumpMessages()
        {
            while (true)
            {
                int orig = transportStatus_;
                if ((orig & MessageQueuedSome) != 0)
                {
                    return;
                }
                const int NetFlagsBoth = MessagePumpInProgress | MessageQueuedSome;
                // Ensure the MessageQueuedSome bit is set.  [?? -> PQ]
                if (Interlocked.CompareExchange(ref transportStatus_, NetFlagsBoth, orig) == orig)
                {
                    if ((orig & MessagePumpInProgress) == 0)
                    {
                        PumpMessagesInternal(); // If [0? -> PQ], then we became the pumper
                    }
                    return;
                }
            }
        }
    }
}
