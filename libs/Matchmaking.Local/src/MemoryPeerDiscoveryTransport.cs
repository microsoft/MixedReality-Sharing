// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;
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

        //TODO extract into a factory so we can have independent networks
        static volatile List<MemoryPeerDiscoveryTransport> instances_ = new List<MemoryPeerDiscoveryTransport>();

        const int NetworkPumpInProgress = 1;
        const int NetworkQueuedSome = 2;
        static int networkStatus_ = 0;

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
            PumpNetwork();
        }

        public void Reply(IPeerDiscoveryMessage req, Guid streamId, ArraySegment<byte> message)
        {
            var r = req as MemoryPeerDiscoveryMessage;
            var m = new MemoryPeerDiscoveryMessage(this, streamId, message);
            r.sender_.incoming_.Enqueue(m);
            PumpNetwork();
        }

        static protected void PumpNetworkInternal()
        {
            // Notation - networkStatus is two bits : P = NetworkPumpInProgress, Q = NetworkQueuedSome
            // The only valid states are [00, P0, PQ], [0Q] is invalid.
            while (true)
            {
                // We're about to process the whole list so clear the NetworkQueuedSome bit. [P? -> P0]
                Interlocked.CompareExchange(ref networkStatus_, NetworkPumpInProgress, NetworkPumpInProgress | NetworkQueuedSome);

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
                            LoggingUtility.LogError("Exception raised while handling message", e);
                        }
                    }
                }

                // Exit if no more work has been queued while we were working. i.e. NetworkQueuedSome is still clear.
                if (Interlocked.CompareExchange(ref networkStatus_, 0, NetworkPumpInProgress) == NetworkPumpInProgress)
                {
                    return; // [P0 -> 00]
                }
            }
        }

        // Called after a Broadcast() or Reply() to ensure the message is delivered.
        // The first thread to send a message becomes the pumper until all messages are deliverd.
        static protected void PumpNetwork()
        {
            while (true)
            {
                int orig = networkStatus_;
                if ((orig & NetworkQueuedSome) != 0)
                {
                    return;
                }
                const int NetFlagsBoth = NetworkPumpInProgress | NetworkQueuedSome;
                // Ensure the NetworkQueuedSome bit is set.  [?? -> PQ]
                if (Interlocked.CompareExchange(ref networkStatus_, NetFlagsBoth, orig) == orig)
                {
                    if ((orig & NetworkPumpInProgress) == 0)
                    {
                        PumpNetworkInternal(); // If [0? -> PQ], then we became the pumper
                    }
                    return;
                }
            }
        }
    }
}
