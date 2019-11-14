// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    class UdpPeerDiscoveryMessage : IPeerDiscoveryMessage
    {
        public Guid StreamId { get; }
        public ArraySegment<byte> Contents { get; }
        internal UdpPeerDiscoveryMessage(EndPoint sender, Guid streamId, ArraySegment<byte> msg)
        {
            Contents = msg;
            StreamId = streamId;
            sender_ = sender;
        }
        internal EndPoint sender_;
    }

    /// <summary>
    /// This class implements transport on UDP broadcast or multicast.
    /// UDP is inherently an unreliable protocol. Reliability decreases exponentially when the packet
    /// becomes large enough to be fragmented (often around 1400 bytes, but sometimes smaller).
    /// Thus this transport is best used for small packet sizes.
    /// </summary>
    public class UdpPeerDiscoveryTransport : IPeerDiscoveryTransport
    {
        private const int LargeMessageLimit = 1400;
        private const int StreamLifetimeMs = 10000;
        private const int StreamCleanupPeriodMs = StreamLifetimeMs;
        private const int MaxRememberedStreams = 10000;

        private Socket socket_;
        private readonly IPEndPoint broadcastEndpoint_;
        private readonly IPAddress localAddress_;
        private readonly EndPoint anywhere_ = new IPEndPoint(IPAddress.Any, 0);
        private readonly byte[] readBuffer_ = new byte[LargeMessageLimit];
        private readonly ArraySegment<byte> readSegment_;

        // Map the ID of each stream for which we are sending messages to the last used sequence number.
        private readonly ConcurrentDictionary<Guid, int> sendStreams_ = new ConcurrentDictionary<Guid, int>();

        private class ReceiveStream
        {
            public int SeqNum = 0;
            public DateTime LastHeard = DateTime.UtcNow; //< TODO use to purge old entries
        }

        // Map the ID of each stream for which we are receiving messages to the highest seen sequence number.
        private readonly Dictionary<Guid, ReceiveStream> receiveStreams_ = new Dictionary<Guid, ReceiveStream>();

        private Task recvTask_;
        private Task deleteExpiredTask_;
        private CancellationTokenSource deleteExpiredCts_;

        /// <summary>
        /// A new message has been received.
        /// </summary>
        public event Action<IPeerDiscoveryTransport, IPeerDiscoveryMessage> Message;

        /// <summary>
        /// The transport has started listening for messages. Called on <see cref="Start()"/>;
        /// </summary>
        public event Action<IPeerDiscoveryTransport> Started;

        /// <summary>
        /// The transport has stopped listening for messages. Called on <see cref="Stop()"/>;
        /// </summary>
        public event Action<IPeerDiscoveryTransport> Stopped;

        /// <summary>
        /// Create a new transport.
        /// </summary>
        /// <param name="broadcast">Broadcast or multicast address used to send packets to other hosts.</param>
        /// <param name="local">Local address.</param>
        /// <param name="port">Port used to send and receive broadcast packets.</param>
        public UdpPeerDiscoveryTransport(IPAddress broadcast, ushort port, IPAddress local = null)
        {
            broadcastEndpoint_ = new IPEndPoint(broadcast, port);
            localAddress_ = local ?? AnyAddress(broadcast.AddressFamily);
            readSegment_ = new ArraySegment<byte>(readBuffer_);
        }

        private IPAddress AnyAddress(AddressFamily family)
        {
            switch (family)
            {
                case AddressFamily.InterNetwork:
                    return IPAddress.Any;
                case AddressFamily.InterNetworkV6:
                    return IPAddress.IPv6Any;
                default:
                    throw new ArgumentException($"Invalid family: {family}");
            }
        }

        private async Task ReceiveLoop()
        {
            while (true)
            {
                Debug.Assert(readSegment_.Offset == 0);
                SocketReceiveFromResult result;
                try
                {
                    result = await socket_.ReceiveFromAsync(readSegment_, SocketFlags.None, anywhere_);
                    HandleMessage(result);
                }
                // Socket has been disposed, terminate.
                catch (NullReferenceException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted || e.SocketErrorCode == SocketError.NotSocket)
                {
                    return;
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.MessageSize)
                {
                    // A datagram too long was received, discard it.
                    continue;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Exception raised while handling message");
                }
            }
        }
        private void HandleMessage(SocketReceiveFromResult result)
        {
            Guid streamId;
            int seqNum = -1; //< unordered
            int payloadOffset;
            using (var str = new MemoryStream(readSegment_.Array, readSegment_.Offset, readSegment_.Count, false))
            using (var reader = new BinaryReader(str))
            {
                streamId = new Guid(reader.ReadBytes(16));
                if (streamId != Guid.Empty)
                {
                    seqNum = reader.ReadInt32();
                }
                payloadOffset = (int)str.Position;
            }

            bool handleMessage = true;
            if (seqNum >= 0)
            {
                // Locking the whole map here just for the sake of deletion is sub-optimal, but
                // we don't expect a high contention so it should be good enough.
                lock (receiveStreams_)
                {
                    if (receiveStreams_.TryGetValue(streamId, out ReceiveStream streamData))
                    {
                        if (seqNum >= streamData.SeqNum)
                        {
                            streamData.SeqNum = seqNum;
                            streamData.LastHeard = DateTime.UtcNow;
                        }
                        else
                        {
                            handleMessage = false;
                        }
                    }
                    else if (receiveStreams_.Count < MaxRememberedStreams)
                    {
                        receiveStreams_.Add(streamId, new ReceiveStream { SeqNum = seqNum });
                    }
                    else
                    {
                        Log.Warning("UdpPeerNetwork.cs: Discarding message from {0} - too many streams", result.RemoteEndPoint);
                        handleMessage = false;
                    }
                }
            }

            if (handleMessage)
            {
                Message?.Invoke(this, new UdpPeerDiscoveryMessage(result.RemoteEndPoint,
                    streamId, new ArraySegment<byte>(readSegment_.Array, payloadOffset, result.ReceivedBytes)));
            }
        }

        private async void CleanupExpired(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(StreamCleanupPeriodMs);

                // Delete all expired entries.
                var expiryTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(StreamLifetimeMs);
                lock (receiveStreams_)
                {
                    var toDelete = receiveStreams_.Where(pair => pair.Value.LastHeard < expiryTime).ToArray();
                    foreach (var entry in toDelete)
                    {
                        receiveStreams_.Remove(entry.Key);
                    }
                }
            }
        }

        public void Start()
        {
            if (socket_ != null)
            {
                throw new InvalidOperationException("Network is already running");
            }

            socket_ = new Socket(broadcastEndpoint_.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

            // Bind to the broadcast port.
            socket_.Bind(new IPEndPoint(localAddress_, broadcastEndpoint_.Port));

            // Optionally join a multicast group
            if (IsMulticast(broadcastEndpoint_.Address))
            {
                if (broadcastEndpoint_.AddressFamily == AddressFamily.InterNetwork)
                {
                    MulticastOption mcastOption;
                    mcastOption = new MulticastOption(broadcastEndpoint_.Address, localAddress_);

                    socket_.SetSocketOption(SocketOptionLevel.IP,
                                                SocketOptionName.AddMembership,
                                                mcastOption);
                }
                else if (broadcastEndpoint_.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    IPv6MulticastOption mcastOption;
                    if (localAddress_ != IPAddress.IPv6Any)
                    {
                        var ifaceIdx = GetIfaceIdxFromAddress(localAddress_);
                        mcastOption = new IPv6MulticastOption(broadcastEndpoint_.Address, ifaceIdx);
                    }
                    else
                    {
                        mcastOption = new IPv6MulticastOption(broadcastEndpoint_.Address);
                    }
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

            // Start the cleanup thread.
            deleteExpiredCts_ = new CancellationTokenSource();
            var token = deleteExpiredCts_.Token;
            deleteExpiredTask_ = Task.Run(() => CleanupExpired(token), token);

            // Start the receiving thread.
            recvTask_ = Task.Run(ReceiveLoop);

            Started?.Invoke(this);
        }

        private static bool IsMulticast(IPAddress address)
        {
            return address.IsIPv6Multicast ||
                (address.AddressFamily == AddressFamily.InterNetwork &&
                (address.GetAddressBytes()[0] >> 4 == 14));
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
            if (socket_ == null)
            {
                throw new InvalidOperationException("Network is not running");
            }

            Stopped?.Invoke(this);

            socket_.Dispose();
            deleteExpiredCts_.Cancel();
            try
            {
                Task.WaitAll(recvTask_, deleteExpiredTask_);
            }
            catch (AggregateException e)
            {
                e.Handle(inner => inner is OperationCanceledException);
            }

            deleteExpiredCts_.Dispose();
            socket_ = null;
        }

        // Prepend stream ID and sequence number.
        byte[] PrependHeader(Guid guid, ArraySegment<byte> message)
        {
            bool ordered = guid != Guid.Empty;
            int size = 16 + message.Count;
            if (ordered)
            {
                size += sizeof(int); //< sequence number.
            }
            var res = new byte[size];

            using (var str = new MemoryStream(res))
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(guid.ToByteArray());
                if (ordered)
                {
                    int seqId = sendStreams_.AddOrUpdate(guid, 0, (_, value) => value + 1);
                    writer.Write(seqId);
                }
                writer.Write(message.Array, message.Offset, message.Count);
            }
            return res;
        }

        public void Broadcast(Guid guid, ArraySegment<byte> message)
        {
            var buffer = PrependHeader(guid, message);
            if (buffer.Length > LargeMessageLimit)
            {
                Log.Warning("UdpPeerNetwork.cs: Large UDP messages will be discarded ({0} bytes)", buffer.Length);
                return;
            }
            socket_.SendTo(buffer, SocketFlags.None, broadcastEndpoint_);
        }

        public void Reply(IPeerDiscoveryMessage req, Guid guid, ArraySegment<byte> message)
        {
            var umsg = req as UdpPeerDiscoveryMessage;
            var buffer = PrependHeader(guid, message);
            if (buffer.Length > LargeMessageLimit)
            {
                Log.Warning("UdpPeerNetwork.cs: Large UDP messages will be discarded ({0} bytes)", buffer.Length);
                return;
            }
            socket_.SendTo(buffer, SocketFlags.None, umsg.sender_);
        }
    }
}
