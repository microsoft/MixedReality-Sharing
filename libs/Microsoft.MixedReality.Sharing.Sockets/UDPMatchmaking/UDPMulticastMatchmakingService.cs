using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.MixedReality.Sharing.Utilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastMatchmakingService : DisposableBase, IMatchmakingService
    {
        private const byte RoomInfoPacket = 1;
        private readonly UDPMulticastSettings multicastSettings;
        private readonly ConcurrentBag<UDPMulticastOwnedRoom> locallyOwnedRooms = new ConcurrentBag<UDPMulticastOwnedRoom>();

        private readonly ConcurrentDictionary<string, Task<UDPMulticastRoom>> discoveredRooms = new ConcurrentDictionary<string, Task<UDPMulticastRoom>>();

        private Socket multicastSocket;

        internal ILogger Logger { get; }

        internal ISessionFactory<UDPMulticastRoomConfiguration> SessionFactory { get; }

        internal IParticipantProvider ParticipantProvider { get; }

        public IReadOnlyCollection<IOwnedRoom> LocallyOwnedRooms { get; }

        public UDPMulticastMatchmakingService(ILogger logger, ISessionFactory<UDPMulticastRoomConfiguration> sessionFactory, IParticipantProvider participantProvider, UDPMulticastSettings multicastSettings)
        {
            if (!multicastSettings.GroupIPAddress.IsValidMulticastAddress())
            {
                throw new ArgumentOutOfRangeException(nameof(multicastSettings.GroupIPAddress), "Not a valid multicast address.");
            }

            Logger = logger;
            SessionFactory = sessionFactory;
            ParticipantProvider = participantProvider;

            this.multicastSettings = multicastSettings;

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
                        // Layout is [1 packetType] [2 bytes : info port][2 bytes : data port][8 bytes - epoch][1-byte : n length][n-bytes : room id]
                        int headerLength = (1 + 2 + 2 + 8 + 1);
                        byte[] buffer = new byte[headerLength + 255];
                        buffer[0] = RoomInfoPacket;
                        using (MemoryStream memoryStream = new MemoryStream(buffer))
                        using (BinaryWriter writer = new BinaryWriter(memoryStream))
                        {
                            foreach (UDPMulticastOwnedRoom room in locallyOwnedRooms)
                            {
                                // Reset to just after first byte
                                memoryStream.Position = 1;

                                writer.Write(room.RoomConfig.InfoPort);
                                writer.Write(room.RoomConfig.DataPort);
                                writer.Write(room.ETag);
                                writer.Write((byte)room.Id.Length);
                                writer.Write(Encoding.ASCII.GetBytes(room.Id));
                                writer.Flush();

                                await multicastSocket.SendToAsync(new ArraySegment<byte>(buffer, 0, (int)memoryStream.Position), SocketFlags.None, multicastGroupEndpoint);
                            }
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
                int headerLength = (1 + 2 + 2 + 8 + 1);
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[headerLength + 255]);
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
                                ProcessRoomInfoPacket(buffer.Array, result.ReceivedBytes, remoteIpEndpoint, cancellationToken);
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

        private void ProcessRoomInfoPacket(byte[] data, int dataLength, IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            // Layout is [1 packetType] [2 bytes : info port][2 bytes : data port][8 bytes - epoch][1-byte : n length][n-bytes : room id]
            int headerLength = (1 + 2 + 2 + 8 + 1);
            if (dataLength < headerLength || data[headerLength - 1] == 0 || dataLength != data[headerLength - 1] + headerLength)
            {
                Logger.LogError($"{nameof(UDPMulticastMatchmakingService)}: Received malformed room info packet.");
                return;
            }

            ushort infoPort, dataPort;
            long etag;
            string groupName;

            using (MemoryStream memoryStream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                memoryStream.Position = 1;

                infoPort = reader.ReadUInt16();
                dataPort = reader.ReadUInt16();
                etag = reader.ReadInt64();
                byte length = reader.ReadByte();
                groupName = Encoding.ASCII.GetString(reader.ReadBytes(length));
            }

            discoveredRooms.AddOrUpdate(groupName,
                addValueFactory: _ => ProcessNewRoomAsync(endPoint, infoPort, dataPort, etag, groupName, cancellationToken),
                updateValueFactory: (_, previous) => ProcessExistingRoomAsync(previous, etag, cancellationToken));
        }

        private async Task<UDPMulticastRoom> ProcessNewRoomAsync(IPEndPoint remoteEndpoint, ushort infoPort, ushort sessionPort, long etag, string roomId, CancellationToken cancellationToken)
        {
            UDPMulticastRoom room = new UDPMulticastRoom(this, new UDPMulticastRoomConfiguration(roomId, remoteEndpoint.Address, infoPort, sessionPort));

            //TODO error handling, etc
            await room.RefreshRoomInfoAsync(etag, cancellationToken);

            return room;
        }

        private async Task<UDPMulticastRoom> ProcessExistingRoomAsync(Task<UDPMulticastRoom> task, long etag, CancellationToken cancellationToken)
        {
            UDPMulticastRoom room = (await task);
            //TODO better
            await room.RefreshRoomInfoAsync(etag, cancellationToken);
            return room;
        }

        public async Task<ISession> JoinRandomSessionAsync(IReadOnlyDictionary<string, string> expectedAttributes, CancellationToken token)
        {
            IEnumerable<IRoom> results = await GetRoomsByAttributesAsync(expectedAttributes, token);

            IRoom room = results.Skip(new Random().Next(results.Count())).First();
            return await room.JoinAsync(token);
        }

        public async Task<ISession> JoinSessionByIdAsync(string roomId, CancellationToken token)
        {
            while (true)
            {
                if (discoveredRooms.TryGetValue(roomId, out Task<UDPMulticastRoom> roomTask))
                {
                    UDPMulticastRoom room = await roomTask.Unless(token);
                    return await room.JoinAsync(token);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }

        public async Task<IEnumerable<IRoom>> GetRoomsByOwnerAsync(IParticipant owner, CancellationToken token)
        {
            return (await Task.WhenAll(discoveredRooms.Values).Unless(token))
                .Where(t => Equals(t.Owner, owner));
        }

        public async Task<IEnumerable<IRoom>> GetRoomsByAttributesAsync(IReadOnlyDictionary<string, string> attributes, CancellationToken token)
        {
            UDPMulticastRoom[] rooms = (await Task.WhenAll(discoveredRooms.Values).Unless(token));

            List<IRoom> toReturn = new List<IRoom>();

            foreach (UDPMulticastRoom room in rooms)
            {
                if (room.Attributes.Count < attributes.Count)
                {
                    continue;
                }

                bool isMatch = true;
                foreach (KeyValuePair<string, string> attribute in attributes)
                {
                    if (!room.Attributes.TryGetValue(attribute.Key, out string foundValue) || attribute.Value != foundValue)
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    toReturn.Add(room);
                }
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
    }
}
