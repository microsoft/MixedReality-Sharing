using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Sockets.UDPMatchmaking;
using Microsoft.MixedReality.Sharing.Sockets.UDPMatchmaking.Messages;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.MixedReality.Sharing.Utilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastMatchmakingService : DisposableBase, IMatchmakingService
    {
        private const byte RoomInfoPacket = 1;
        private readonly UDPMulticastSettings multicastSettings;
        private readonly ConcurrentBag<UDPMulticastOwnedRoom> locallyOwnedRooms = new ConcurrentBag<UDPMulticastOwnedRoom>();

        private readonly ConcurrentDictionary<Guid, UDPMulticastRoom> discoveredRooms = new ConcurrentDictionary<Guid, UDPMulticastRoom>();
        private readonly List<MulticastRefreshableCollection> refreshableCollections = new List<MulticastRefreshableCollection>();

        private Socket multicastSocket;

        internal ILogger Logger { get; }

        internal ISessionFactory<UDPMulticastRoomConfiguration> SessionFactory { get; }

        internal IParticipantProvider ParticipantProvider { get; }

        internal SynchronizationContext SynchronizationContext { get; }

        public IReadOnlyCollection<IOwnedRoom> LocallyOwnedRooms { get; }

        public UDPMulticastMatchmakingService(ILogger logger, ISessionFactory<UDPMulticastRoomConfiguration> sessionFactory, IParticipantProvider participantProvider, UDPMulticastSettings multicastSettings, SynchronizationContext synchronizationContext = null)
        {
            if (!multicastSettings.GroupIPAddress.IsValidMulticastAddress())
            {
                throw new ArgumentOutOfRangeException(nameof(multicastSettings.GroupIPAddress), "Not a valid multicast address.");
            }

            Logger = logger;
            SessionFactory = sessionFactory;
            ParticipantProvider = participantProvider;

            this.multicastSettings = multicastSettings;
            SynchronizationContext = synchronizationContext ?? SynchronizationContext.Current;

            LocallyOwnedRooms = new ReadOnlyCollectionWrapper<IOwnedRoom>(locallyOwnedRooms);

            Task.Run(() => StartBroadcast(DisposeCancellationToken), DisposeCancellationToken).FireAndForget();

            EnsureSocket();
        }

        private async Task StartBroadcast(CancellationToken cancellationToken)
        {
            IPEndPoint multicastGroupEndpoint = new IPEndPoint(multicastSettings.GroupIPAddress, multicastSettings.MulticastPort);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (locallyOwnedRooms.Count > 0)
                    {
                        byte[] buffer = new byte[AnnounceMessage.Size + 1];
                        buffer[0] = AnnounceMessage.MessageTypeId;
                        ArraySegment<byte> toSend = new ArraySegment<byte>(buffer);
                        foreach (UDPMulticastOwnedRoom room in locallyOwnedRooms)
                        {
                            // Reset to just after first byte
                            AnnounceMessage message = new AnnounceMessage(room.Id, room.ETag, room.RoomConfig.DataPort, room.RoomConfig.InfoPort);
                            message.ToBytes(buffer, 1);
                            await multicastSocket.SendToAsync(toSend, SocketFlags.None, multicastGroupEndpoint);
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (ObjectDisposedException) { }
        }

        private void EnsureSocket()
        {
            multicastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            multicastSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

            IPEndPoint localEndpoint = new IPEndPoint(multicastSettings.LocalIPAddress, multicastSettings.MulticastPort);
            IPEndPoint multicastGroupEndpoint = new IPEndPoint(multicastSettings.GroupIPAddress, multicastSettings.MulticastPort);

            multicastSocket.Bind(localEndpoint);

            multicastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastGroupEndpoint.Address, localEndpoint.Address));

            Task.Run(() => ReceiveDataAsync(DisposeCancellationToken), DisposeCancellationToken).FireAndForget();
        }

        protected override void OnManagedDispose()
        {
            multicastSocket?.Close();
            multicastSocket?.Dispose();

            foreach (UDPMulticastOwnedRoom room in locallyOwnedRooms)
            {
                room.Dispose();
            }
        }

        private async Task ReceiveDataAsync(CancellationToken cancellationToken)
        {
            IPEndPoint multicastGroupEndpoint = new IPEndPoint(multicastSettings.GroupIPAddress, multicastSettings.MulticastPort);

            try
            {
                // We only have 1 message, so just fit the buffer to it
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1 + AnnounceMessage.Size]);

                while (!cancellationToken.IsCancellationRequested)
                {
                    SocketReceiveFromResult result = await multicastSocket.ReceiveFromAsync(buffer, SocketFlags.None, multicastGroupEndpoint);

                    if (!(result.RemoteEndPoint is IPEndPoint remoteIpEndpoint))
                    {
                        Logger.LogError($"{nameof(UDPMulticastMatchmakingService)}: Received UDP Multicast packet from unknown endpoint type, address family '{result.RemoteEndPoint.AddressFamily}'.");
                        continue;
                    }

                    if (result.ReceivedBytes > 0)
                    {
                        switch (buffer.Array[0])
                        {
                            case RoomInfoPacket:
                            {
                                if (result.ReceivedBytes != 1 + AnnounceMessage.Size)
                                {
                                    Logger.LogError($"{nameof(UDPMulticastMatchmakingService)}: Received malformed room info packet.");
                                }
                                else
                                {
                                    AnnounceMessage announceMessage = buffer.Array.AsStruct<AnnounceMessage>(1);
                                    UDPMulticastRoom room = discoveredRooms.GetOrAdd(announceMessage.Id, new UDPMulticastRoom(this, new UDPMulticastRoomConfiguration(announceMessage.Id, remoteIpEndpoint.Address, announceMessage.InfoPort, announceMessage.DataPort)));
                                    ProcessRoomUpdateAsync(announceMessage, room, cancellationToken).FireAndForget();
                                }
                            }
                            break;
                            default:
                                Logger.LogError($"{nameof(UDPMulticastMatchmakingService)}: Received unknown UDP Multicast packet with header byte '{buffer.Array[0]}', ignoring.");
                                break;
                        }
                    }
                }
            }
            catch (ObjectDisposedException) { }
        }

        private async Task ProcessRoomUpdateAsync(AnnounceMessage announceMessage, UDPMulticastRoom room, CancellationToken cancellationToken)
        {
            bool succeded = await room.RefreshRoomInfoAsync(announceMessage.ETag, cancellationToken);

            if (succeded)
            {
                lock (refreshableCollections)
                {
                    refreshableCollections.ForEach(t => t.CheckForUpdate(room));
                }
            }
        }

        public async Task<ISession> JoinRandomSessionAsync(IReadOnlyDictionary<string, string> expectedAttributes, CancellationToken token)
        {
            List<UDPMulticastRoom> results = new List<UDPMulticastRoom>();
            foreach (UDPMulticastRoom room in discoveredRooms.Values)
            {
                if (CheckRoomForAttributeMatch(room, expectedAttributes))
                {
                    results.Add(room);
                }
            }

            if (results.Count == 0)
            {
                return null;
            }

            return await results[new Random().Next(results.Count)].JoinAsync(token);
        }

        public async Task<ISession> JoinSessionByIdAsync(string roomId, CancellationToken token)
        {
            while (true)
            {
                if (discoveredRooms.TryGetValue(Guid.Parse(roomId), out UDPMulticastRoom room))
                {
                    return await room.JoinAsync(token);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }

        public IRefreshableCollection<IRoom> GetRoomsByOwnerAsync(IParticipant owner)
        {
            MulticastRefreshableCollection toReturn = new MulticastRefreshableCollection(r => r.Owner?.Id == owner?.Id, SynchronizationContext);

            foreach (UDPMulticastRoom room in discoveredRooms.Values)
            {
                toReturn.CheckForUpdate(room);
            }

            lock (refreshableCollections)
            {
                refreshableCollections.Add(toReturn);
            }

            return toReturn;
        }

        public IRefreshableCollection<IRoom> GetRoomsByAttributesAsync(IReadOnlyDictionary<string, string> attributes)
        {
            MulticastRefreshableCollection toReturn = new MulticastRefreshableCollection(r => CheckRoomForAttributeMatch(r, attributes), SynchronizationContext);

            foreach (UDPMulticastRoom room in discoveredRooms.Values)
            {
                toReturn.CheckForUpdate(room);
            }

            lock (refreshableCollections)
            {
                refreshableCollections.Add(toReturn);
            }

            return toReturn;
        }

        public async Task<IOwnedRoom> OpenRoomAsync(IDictionary<string, string> attributes, CancellationToken token)
        {
            ThrowIfDisposed();

            KeyValuePair<UDPMulticastRoomConfiguration, ISession> result = await SessionFactory.HostNewRoomAsync(attributes, token);
            UDPMulticastOwnedRoom toReturn = new UDPMulticastOwnedRoom(this, result.Key, result.Value, attributes);
            locallyOwnedRooms.Add(toReturn);
            toReturn.StartHosting(DisposeCancellationToken);

            return toReturn;
        }

        private bool CheckRoomForAttributeMatch(UDPMulticastRoom room, IReadOnlyDictionary<string, string> attributes)
        {
            foreach (KeyValuePair<string, string> attribute in attributes)
            {
                if (!room.Attributes.TryGetValue(attribute.Key, out string foundValue) || attribute.Value != foundValue)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
