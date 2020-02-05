// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Test
{
    // Utility for making multiple instances of IPeerDiscoveryTransport communicating with each other.
    internal interface ITransportBuilder
    {
        IPeerDiscoveryTransport MakeTransport(int userIndex);
        bool SimulatesPacketLoss { get; }
    }

    internal class MemoryTransportBuilder : ITransportBuilder
    {
        private readonly ushort port_ = Utils.NewPortNumber.Calculate();

        public IPeerDiscoveryTransport MakeTransport(int userIndex)
        {
            return new MemoryPeerDiscoveryTransport(port_);
        }

        public bool SimulatesPacketLoss => false;
    }

    internal class UdpTransportBuilder : ITransportBuilder
    {
        private readonly ushort port_ = Utils.NewPortNumber.Calculate();

        public IPeerDiscoveryTransport MakeTransport(int userIndex)
        {
            return new UdpPeerDiscoveryTransport(new IPAddress(0xffffff7f), port_, new IPAddress(0x0000007f + (userIndex << 24)));
        }
        public bool SimulatesPacketLoss => false;
    }

    internal class UdpMulticastTransportBuilder : ITransportBuilder
    {
        private readonly ushort port_ = Utils.NewPortNumber.Calculate();

        public IPeerDiscoveryTransport MakeTransport(int userIndex)
        {
            return new UdpPeerDiscoveryTransport(new IPAddress(0x000000e0), port_, new IPAddress(0x0000007f + (userIndex << 24)));
        }
        public bool SimulatesPacketLoss => false;
    }

    internal class UdpReorderedTransportBuilder : ITransportBuilder, IDisposable
    {
        public const int MaxDelayMs = 25;
        public const int MaxRetries = 3;

        private readonly Socket relay_;
        private readonly ushort port_ = Utils.NewPortNumber.Calculate();
        private readonly List<IPEndPoint> recipients_ = new List<IPEndPoint>();

        private readonly Random random_;

        // Wraps a packet for use in a map.
        private class Packet
        {
            public IPEndPoint EndPoint;
            public byte[] Contents;

            public Packet(IPEndPoint endPoint, ArraySegment<byte> contents)
            {
                EndPoint = endPoint;
                Contents = new byte[contents.Count];
                for (int i = 0; i < contents.Count; ++i)
                {
                    Contents[i] = contents[i];
                }
            }

            public override bool Equals(object other)
            {
                if (other is Packet rhs)
                {
                    return EndPoint.Equals(rhs.EndPoint) && Contents.SequenceEqual(rhs.Contents);
                }
                return false;
            }

            public override int GetHashCode()
            {
                // Not a great hash but it shouldn't matter in this case.
                var result = 0;
                foreach (byte b in Contents)
                {
                    result = (result * 31) ^ b;
                }
                return EndPoint.GetHashCode() ^ result;
            }
        }

        // Keeps track of each packet that goes through the relay and counts its repetitions.
        // See receive loop for usage.
        private readonly Dictionary<Packet, int> packetCounters;

        public bool SimulatesPacketLoss => (packetCounters != null);
        public IPeerDiscoveryTransport MakeTransport(int userIndex)
        {
            // Peers all send packets to the relay.
            var address = new IPAddress(0x0000007f + (userIndex << 24));
            lock (recipients_)
            {
                var endpoint = new IPEndPoint(address, port_);

                // Remove first so the same agent can be re-created multiple times
                recipients_.Remove(endpoint);
                recipients_.Add(endpoint);
            }
            return new UdpPeerDiscoveryTransport(new IPAddress(0xfeffff7f), port_, address,
                new UdpPeerDiscoveryTransport.Options { MaxRetries = MaxRetries, MaxRetryDelayMs = 100 });
        }

        public UdpReorderedTransportBuilder(Random random, bool packetLoss)
        {
            random_ = random;

            relay_ = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            relay_.Bind(new IPEndPoint(new IPAddress(0xfeffff7f), port_));

            // Disable exception on UDP connection reset (don't care).
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            relay_.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

            if (packetLoss)
            {
                packetCounters = new Dictionary<Packet, int>();
            }

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        byte[] buf_ = new byte[1024];
                        var result = await relay_.ReceiveFromAsync(new ArraySegment<byte>(buf_), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));

                        if (packetCounters != null)
                        {
                            // Increase the probability of delivery of a packet with retries, up to 100% on the last retry.
                            // This simulates heavy packet loss while still guaranteeing that everything works.
                            // Note that this logic is very naive, but should be fine for small tests.
                            var packet = new Packet((IPEndPoint)result.RemoteEndPoint, new ArraySegment<byte>(buf_, 0, result.ReceivedBytes));
                            if (!packetCounters.TryGetValue(packet, out int counter))
                            {
                                counter = 0;
                            }

                            if (counter == MaxRetries - 1)
                            {
                                // Last retry, always send and forget the packet.
                                packetCounters.Remove(packet);
                            }
                            else
                            {
                                packetCounters[packet] = counter + 1;
                                // Drop with decreasing probability.
                                if (random_.Next(0, MaxRetries) > counter)
                                {
                                    continue;
                                }
                            }
                        }

                        // The relay sends the packets to all peers with a random delay.
                        IPEndPoint[] curRecipients;
                        lock (recipients_)
                        {
                            curRecipients = recipients_.ToArray();
                        }
                        foreach (var rec in curRecipients)
                        {
                            var delay = random_.Next(MaxDelayMs);
                            _ = Task.Delay(delay).ContinueWith(t =>
                            {
                                try
                                {
                                    relay_.SendToAsync(new ArraySegment<byte>(buf_, 0, result.ReceivedBytes), SocketFlags.None, rec);
                                }
                                catch (ObjectDisposedException) { }
                                catch (SocketException e) when (e.SocketErrorCode == SocketError.NotSocket) { }
                            });
                        }
                    }
                }
                catch (ObjectDisposedException) { }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.NotSocket) { }
            });
        }

        public void Dispose()
        {
            relay_.Dispose();
        }
    }
}
