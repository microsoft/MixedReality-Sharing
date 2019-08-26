// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

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
        static volatile bool pumpingNetwork_ = false;

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

        static protected void PumpNetwork()
        {
            if (pumpingNetwork_)
            {
                return;
            }
            pumpingNetwork_ = true;
            bool workDone = true;
            while (workDone)
            {
                workDone = false;
                foreach (var c in instances_)
                {
                    MemoryPeerNetworkMessage msg;
                    while (c.incoming_.TryDequeue(out msg))
                    {
                        c.Message.Invoke(c, msg);
                        workDone = true;
                    }
                }
            }
            pumpingNetwork_ = false;
        }
    }
}
