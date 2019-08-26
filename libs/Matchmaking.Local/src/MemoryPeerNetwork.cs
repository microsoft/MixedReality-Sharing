// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    class MemoryPeerNetworkMessage : IPeerNetworkMessage
    {
        public byte[] Message { get; }
        internal MemoryPeerNetworkMessage(MemoryPeerNetwork sender, byte[] msg)
        {
            Message = msg;
            sender_ = sender;
        }
        internal MemoryPeerNetwork sender_;
    }

    public class MemoryPeerNetwork : IPeerNetwork
    {
        int ident_;
        ConcurrentQueue<MemoryPeerNetworkMessage> incoming_ = new ConcurrentQueue<MemoryPeerNetworkMessage>();

        //TODO extract into a factory so we can have independent networks
        static volatile List<MemoryPeerNetwork> instances_ = new List<MemoryPeerNetwork>();

        const int NetworkPumpInProgress = 1;
        const int NetworkQueuedSome = 2;
        static int networkStatus_ = 0;

        public MemoryPeerNetwork(int ident)
        {
            ident_ = ident;
        }

        public void Start()
        {
            // In Start() and Stop() we replace the entire list so that we don't invalidate
            // any existing iterators.
            lock (instances_)
            {
                Debug.Assert(instances_.Contains(this) == false);
                var i = new List<MemoryPeerNetwork>(instances_);
                i.Add(this);
                instances_ = i;
            }
        }

        public void Stop()
        {
            lock (instances_)
            {
                Debug.Assert(instances_.Contains(this));
                var i = new List<MemoryPeerNetwork>(instances_);
                i.Remove(this);
                instances_ = i;
            }
        }

        public event Action<IPeerNetwork, IPeerNetworkMessage> Message;

        public void Broadcast(byte[] message)
        {
            var m = new MemoryPeerNetworkMessage(this, message);
            foreach (var c in instances_)
            {
                if (c != this)
                {
                    c.incoming_.Enqueue(m);
                }
            }
            PumpNetwork();
        }

        public void Reply(IPeerNetworkMessage req, byte[] message)
        {
            var r = req as MemoryPeerNetworkMessage;
            var m = new MemoryPeerNetworkMessage(this, message);
            r.sender_.incoming_.Enqueue(m);
            PumpNetwork();
        }

        static protected void PumpNetworkInternal()
        {
            // Notation - networkStatus is two bits : P = NetworkPumpInProgress, Q = NetworkPumpInProgress
            // The only valid states are [00, P0, PQ], [0Q] is invalid.
            while (true)
            {
                // We're about to process the whole list so clear the NetworkQueuedSome bit. [P? -> P0]
                Interlocked.CompareExchange(ref networkStatus_, NetworkPumpInProgress, NetworkPumpInProgress | NetworkQueuedSome);

                // Raise the messsage events
                foreach (var c in instances_)
                {
                    MemoryPeerNetworkMessage msg;
                    while (c.incoming_.TryDequeue(out msg))
                    {
                        c.Message.Invoke(c, msg);
                    }
                }

                // Exit if no more work has been queued while we were working. i.e. NetworkQueuedSome is still clear.
                if (Interlocked.CompareExchange(ref networkStatus_, 0, NetworkPumpInProgress) == 0)
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
                const int NetFlagsBoth = NetworkPumpInProgress | NetworkQueuedSome;
                // Ensure the NetworkQueuedSome bit is set.  [?? -> PQ]
                if( Interlocked.CompareExchange(ref networkStatus_, NetFlagsBoth, orig) == NetFlagsBoth )
                {
                    if( (orig & NetworkPumpInProgress) == 0 )
                    {
                        PumpNetworkInternal(); // If [0? -> PQ], then we became the pumper
                    }
                    return;
                }
            }           
        }
    }
}
