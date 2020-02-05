// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly int port_;
        private readonly ConcurrentQueue<MemoryPeerDiscoveryMessage> incoming_ = new ConcurrentQueue<MemoryPeerDiscoveryMessage>();

        private static readonly ConcurrentDictionary<int, List<MemoryPeerDiscoveryTransport>> instances_ =
            new ConcurrentDictionary<int, List<MemoryPeerDiscoveryTransport>>();

        private const int MessagePumpInProgress = 1;
        private const int MessageQueuedSome = 2;
        private static int transportStatus_ = 0;

        /// <summary>
        /// Creates a transport on the specific "port" (broadcast group).
        /// </summary>
        public MemoryPeerDiscoveryTransport(int port)
        {
            port_ = port;
        }

        public void Start()
        {
            // In Start() and Stop() we replace the entire list so that we don't invalidate
            // any existing iterators.
            instances_.AddOrUpdate(port_, new List<MemoryPeerDiscoveryTransport> { this },
                (port, existing) =>
                {
                    if (existing.Contains(this))
                    {
                        throw new InvalidOperationException("Transport is already started");
                    }
                    var res = new List<MemoryPeerDiscoveryTransport>(existing);
                    res.Add(this);
                    return res;
                });
        }

        public void Stop()
        {
            bool succeeded = false;
            while (!succeeded)
            {
                bool started = instances_.TryGetValue(port_, out List<MemoryPeerDiscoveryTransport> oldTransports) &&
                    oldTransports.Contains(this);
                if (!started)
                {
                    throw new InvalidOperationException("Transport is already stopped");
                }
                var newTransports = oldTransports.Where(t => t != this).ToList();
                succeeded = instances_.TryUpdate(port_, newTransports, oldTransports);
            }
        }

        public event Action<IPeerDiscoveryTransport, IPeerDiscoveryMessage> Message;

        public void Broadcast(Guid streamId, ArraySegment<byte> message)
        {
            var m = new MemoryPeerDiscoveryMessage(this, streamId, message);
            var transports = instances_[port_];
            foreach (var c in transports)
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
                foreach (var domain in instances_)
                {
                    foreach (var c in domain.Value)
                    {
                        MemoryPeerDiscoveryMessage msg;
                        while (c.incoming_.TryDequeue(out msg))
                        {
                            try
                            {
                                c.Message?.Invoke(c, msg);
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Exception raised while handling message");
                            }
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
