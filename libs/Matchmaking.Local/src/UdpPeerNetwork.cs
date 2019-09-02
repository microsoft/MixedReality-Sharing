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

        private readonly EndPoint anywhere_ = new IPEndPoint(IPAddress.Any, 0);
        private readonly byte[] readBuffer_ = new byte[1024];
        private readonly ArraySegment<byte> readSegment_;

        public UdpPeerNetwork(EndPoint broadcast, EndPoint local)
        {
            readSegment_ = new ArraySegment<byte>(readBuffer_);
            broadcastEndpoint_ = broadcast;
            localEndpoint_ = local;
        }

        private void HandleAsyncRead(Task<SocketReceiveFromResult> task)
        {
            if (task.IsFaulted)
            {
                task.Exception.Handle(e =>
                {
                    // If the socket has been closed, quit gracefully.
                    return e is ObjectDisposedException;
                });
                return;
            }

            // Dispatch the message.
            var result = task.Result;
            var buf = new byte[result.ReceivedBytes];
            Array.Copy(readBuffer_, 0, buf, 0, buf.Length);
            Message?.Invoke(this, new UdpPeerNetworkMessage(result.RemoteEndPoint, buf));

            // Listen again.
            socket_.ReceiveFromAsync(readSegment_, SocketFlags.None, anywhere_)
                .ContinueWith(HandleAsyncRead);
        }

        public void Start()
        {
            Debug.Assert(socket_ == null);
            socket_ = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket_.Bind(localEndpoint_);
            socket_.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

            socket_.ReceiveFromAsync(readSegment_, SocketFlags.None, anywhere_)
                .ContinueWith(HandleAsyncRead);
        }

        public void Stop()
        {
            socket_.Dispose();
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
