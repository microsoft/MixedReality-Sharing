// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    class UdpPeerNetworkMessage : IPeerNetworkMessage
    {
        public byte[] Message { get; }
        internal UdpPeerNetworkMessage(EndPoint sender, byte[] msg)
        {
            Message = msg;
            sender_ = sender;
        }
        internal EndPoint sender_;
    }

    public class UdpPeerNetwork : IPeerNetwork
    {
        Socket socket_;
        EndPoint broadcastEndpoint_;
        EndPoint localEndpoint_;
        class AsyncReadData
        {
            internal byte[] buffer_ = new byte[1024];
            internal EndPoint sender_ = new IPEndPoint(IPAddress.Any, 0);
        }
        AsyncReadData asyncReadData_ = new AsyncReadData();

        public UdpPeerNetwork(EndPoint broadcast, EndPoint local)
        {
            broadcastEndpoint_ = broadcast;
            localEndpoint_ = local;
        }

        private void HandleAsyncRead(IAsyncResult ar)
        {
            Debug.Assert(ar.AsyncState == asyncReadData_);
            int nb;
            try
            {
                nb = socket_.EndReceiveFrom(ar, ref asyncReadData_.sender_);
            }
            catch (ObjectDisposedException e)
            {
                return;
            }
            Debug.Assert(nb > 0);
            var buf = new byte[nb];
            Array.Copy(asyncReadData_.buffer_, 0, buf, 0, nb);
            Message.Invoke(this, new UdpPeerNetworkMessage(asyncReadData_.sender_, buf));

            // listen again
            var s = asyncReadData_.sender_ as IPEndPoint;
            s.Address = IPAddress.Any;
            s.Port = 0;
            socket_.BeginReceiveFrom(asyncReadData_.buffer_, 0, 1024, SocketFlags.None,
                ref asyncReadData_.sender_, HandleAsyncRead, asyncReadData_);
        }

        public void Start()
        {
            Debug.Assert(socket_ == null);
            socket_ = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket_.Bind(localEndpoint_);
            socket_.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

            socket_.BeginReceiveFrom(asyncReadData_.buffer_, 0, 1024, SocketFlags.None,
                ref asyncReadData_.sender_, HandleAsyncRead, asyncReadData_);
        }

        public void Stop()
        {
            socket_.Close();
            socket_.Dispose();
            socket_ = null;
        }

        public event Action<IPeerNetwork, IPeerNetworkMessage> Message;

        public void Broadcast(byte[] msg)
        {
            socket_.SendTo(msg, broadcastEndpoint_);
        }

        public void Reply(IPeerNetworkMessage req, byte[] msg)
        {
            var umsg = req as UdpPeerNetworkMessage;
            socket_.SendTo(msg, umsg.sender_);
        }
    }
}
