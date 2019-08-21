// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    class MemoryFooMessage : IPeerNetworkMessage
    {
        public byte[] Message { get; }
        internal MemoryFooMessage(MemoryPeerNetwork sender, byte[] msg)
        {
            Message = msg;
            sender_ = sender;
        }
        internal MemoryPeerNetwork sender_;
    }

    public class MemoryPeerNetwork : IPeerNetwork
    {
        int ident_;
        Queue<MemoryFooMessage> incoming_ = new Queue<MemoryFooMessage>();

        //TODO extract into a factory so we can have independent networks
        static List<MemoryPeerNetwork> instances_ = new List<MemoryPeerNetwork>();
        static bool pumpingNetwork_ = false;

        public MemoryPeerNetwork(int ident)
        {
            ident_ = ident;
        }

        public void Start()
        {
            Debug.Assert(instances_.Contains(this) == false);
            instances_.Add(this);
        }

        public void Stop()
        {
            Debug.Assert(instances_.Contains(this));
            instances_.Remove(this);
        }

        public event Action<IPeerNetwork, IPeerNetworkMessage> Message;

        public void Broadcast(byte[] msg)
        {
            var m = new MemoryFooMessage(this, msg);
            foreach (var c in instances_)
            {
                if( c != this )
                {
                    c.incoming_.Enqueue(m);
                }
            }
            PumpNetwork();
        }

        public void Reply(IPeerNetworkMessage req, byte[] msg)
        {
            var r = req as MemoryFooMessage;
            var m = new MemoryFooMessage(this, msg);
            r.sender_.incoming_.Enqueue(m);
            PumpNetwork();
        }

        static void PumpNetwork()
        {
            if( pumpingNetwork_ )
            {
                return;
            }
            pumpingNetwork_ = true;
            bool workDone = true;
            while(workDone)
            {
                workDone = false;
                foreach( var c in instances_)
                {
                    while( c.incoming_.Count > 0 )
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
