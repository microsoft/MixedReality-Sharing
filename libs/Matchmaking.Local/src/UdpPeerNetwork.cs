// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
        private Socket socket_;
        private readonly IPEndPoint broadcastEndpoint_;
        private readonly IPEndPoint localEndpoint_;
        private readonly JoinMulticastGroup joinMulticastGroup_;
        private readonly EndPoint anywhere_ = new IPEndPoint(IPAddress.Any, 0);
        private readonly byte[] readBuffer_ = new byte[1024];
        private readonly ArraySegment<byte> readSegment_;

        public event Action<IPeerNetwork, IPeerNetworkMessage> Message;

        public enum JoinMulticastGroup { DoNotJoin, Join }

        /// <summary>
        /// Create a new network.
        /// </summary>
        /// <param name="broadcast">Broadcast or multicast address used to send packets to other hosts.</param>
        /// <param name="local">Local address that the network socket will bind to.</param>
        /// <param name="joinMulticastGroup">
        /// If <see cref="JoinMulticastGroup.Yes"/>, the network socket will join the multicast group corresponding to
        /// `broadcast`; otherwise `broadcast` will be interpreted as a local broadcast address.</param>
        public UdpPeerNetwork(IPEndPoint broadcast, IPEndPoint local, JoinMulticastGroup joinMulticastGroup = JoinMulticastGroup.DoNotJoin)
        {
            broadcastEndpoint_ = broadcast;
            localEndpoint_ = local;
            joinMulticastGroup_ = joinMulticastGroup;
            readSegment_ = new ArraySegment<byte>(readBuffer_);
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
            socket_ = new Socket(broadcastEndpoint_.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket_.Bind(localEndpoint_);

            if (joinMulticastGroup_ == JoinMulticastGroup.Join)
            {
                if (broadcastEndpoint_.AddressFamily == AddressFamily.InterNetwork)
                {
                    MulticastOption mcastOption;
                    mcastOption = new MulticastOption(broadcastEndpoint_.Address, localEndpoint_.Address);

                    socket_.SetSocketOption(SocketOptionLevel.IP,
                                                SocketOptionName.AddMembership,
                                                mcastOption);
                }
                else if (broadcastEndpoint_.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    IPv6MulticastOption mcastOption;
                    var ifaceIdx = GetIfaceIdxFromAddress(localEndpoint_.Address);
                    mcastOption = new IPv6MulticastOption(broadcastEndpoint_.Address, ifaceIdx);

                    socket_.SetSocketOption(SocketOptionLevel.IP,
                                                SocketOptionName.AddMembership,
                                                mcastOption);
                }
                else
                {
                    // Should never happen
                    throw new NotSupportedException($"Invalid address family: {broadcastEndpoint_.AddressFamily}");
                }
            }
            else
            {
                // Assume this is a broadcast address.
                socket_.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            }

            socket_.ReceiveFromAsync(readSegment_, SocketFlags.None, anywhere_)
                .ContinueWith(HandleAsyncRead);
        }

        private static long GetIfaceIdxFromAddress(IPAddress localAddress)
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces();
            var found = ifaces.First(iface =>
                        iface.GetIPProperties().UnicastAddresses.Any(
                            addressInfo => addressInfo.Address.Equals(localAddress)));
            return found.GetIPProperties().GetIPv6Properties().Index;
        }

        public void Stop()
        {
            socket_.Dispose();
        }

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
