// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
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
        Queue<MemoryPeerNetworkMessage> incoming_ = new Queue<MemoryPeerNetworkMessage>();

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

        public void Broadcast(byte[] msg)
        {
            var m = new MemoryPeerNetworkMessage(this, msg);
            foreach (var c in instances_)
            {
                if (c != this)
                {
                    c.incoming_.Enqueue(m);
                }
            }
            PumpNetwork();
        }

        public void Reply(IPeerNetworkMessage req, byte[] msg)
        {
            var r = req as MemoryPeerNetworkMessage;
            var m = new MemoryPeerNetworkMessage(this, msg);
            r.sender_.incoming_.Enqueue(m);
            PumpNetwork();
        }

        static void PumpNetwork()
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
                    while (c.incoming_.Count > 0)
                    {
                        var m = c.incoming_.Dequeue();
                        c.Message.Invoke(c, m);
                        workDone = true;
                    }
                }
            }
            pumpingNetwork_ = false;
        }
    }
}
