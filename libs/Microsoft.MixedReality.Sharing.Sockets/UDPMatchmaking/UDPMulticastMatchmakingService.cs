using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.MixedReality.Sharing.Utilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentBag<UDPMulticastEditableRoom> locallyOwnedRooms = new ConcurrentBag<UDPMulticastEditableRoom>();

        private readonly ConcurrentDictionary<string, Task<UDPMulticastRoom>> discoveredRooms = new ConcurrentDictionary<string, Task<UDPMulticastRoom>>();

        private Socket multicastSocket;

        internal ILogger Logger { get; }

        internal ISessionFactory<UDPMulticastRoomConfiguration> SessionFactory { get; }

        internal IParticipantProvider ParticipantProvider { get; }

        public IReadOnlyCollection<IEditableRoom> LocallyOwnedRooms { get; }

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

            LocallyOwnedRooms = new ReadOnlyCollectionWrapper<IEditableRoom>(locallyOwnedRooms);

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
                        // Layout is [1 header] [2 bytes : info port][2 bytes : session port][1-byte : n length][n-bytes : room id]
                        byte[] buffer = new byte[261];
                        buffer[0] = RoomInfoPacket;
                        foreach (UDPMulticastEditableRoom room in locallyOwnedRooms)
                        {
                            ArraySegment<byte> toSend = new ArraySegment<byte>(buffer, 0, 6 + room.Id.Length);
                            buffer.SetAsUInt16LittleIndian(room.RoomConfig.InfoPort, 1);
                            buffer.SetAsUInt16LittleIndian(room.RoomConfig.InfoPort, 3);
                            buffer[5] = (byte)room.Id.Length;
                            Encoding.ASCII.GetBytes(room.Id).CopyTo(buffer, 6);

                            await multicastSocket.SendToAsync(toSend, SocketFlags.None, multicastGroupEndpoint);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
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

            foreach (UDPMulticastEditableRoom room in locallyOwnedRooms)
            {
                room.Close();
            }
        }

        private async Task ReceiveDataAsync(CancellationToken cancellationToken)
        {
            IPEndPoint multicastGroupEndpoint = new IPEndPoint(multicastSettings.GroupIPAddress, multicastSettings.MulticastPort);

            try
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[261]);
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
            // Layout is [1 header] [2 bytes : info port][2 bytes : session port][1-byte : n length][n-bytes : room id]
            if (dataLength < 6 || data[5] == 0 || dataLength != data[5] + 6)
            {
                Logger.LogError($"{nameof(UDPMulticastMatchmakingService)}: Received malformed room info packet.");
                return;
            }

            ushort infoPort = data.GetUInt16LittleEndian(1);
            ushort sessionPort = data.GetUInt16LittleEndian(3);
            string groupName = Encoding.ASCII.GetString(data, 6, data[5]);

            discoveredRooms.GetOrAdd(groupName, _ => ProcessNewRoomAsync(endPoint, infoPort, sessionPort, groupName, cancellationToken));
        }

        private async Task<UDPMulticastRoom> ProcessNewRoomAsync(IPEndPoint remoteEndpoint, ushort infoPort, ushort sessionPort, string roomId, CancellationToken cancellationToken)
        {
            UDPMulticastRoom room = new UDPMulticastRoom(this, new UDPMulticastRoomConfiguration(roomId, infoPort, sessionPort), remoteEndpoint.Address);

            //TODO error handling, etc
            await room.RefreshRoomInfoAsync(cancellationToken);

            return room;
        }

        public async Task<IRoom> GetRandomRoomAsync(IReadOnlyDictionary<string, string> expectedAttributes, CancellationToken token)
        {
            IEnumerable<IRoom> results = await GetRoomsByAttributesAsync(expectedAttributes, token);

            return results.Skip(new Random().Next(results.Count())).First();
        }

        public async Task<IRoom> GetRoomByIdAsync(string roomId, CancellationToken token)
        {
            while (true)
            {
                if (discoveredRooms.TryGetValue(roomId, out Task<UDPMulticastRoom> roomTask))
                {
                    return await roomTask.Unless(token);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }

        public async Task<IEnumerable<IRoom>> GetRoomsByOwnerAsync(IParticipant owner, CancellationToken token)
        {
            return (await Task.WhenAll(discoveredRooms.Values).Unless(token))
                .Where(t => Equals(t.Owner, owner));
        }

        public async Task<IEnumerable<IRoom>> GetRoomsByParticipantsAsync(IEnumerable<IParticipant> participants, CancellationToken token)
        {
            UDPMulticastRoom[] rooms = (await Task.WhenAll(discoveredRooms.Values).Unless(token));

            HashSet<IParticipant> participantsToFind = new HashSet<IParticipant>(participants);
            List<IRoom> toReturn = new List<IRoom>();

            foreach (UDPMulticastRoom room in rooms)
            {
                if (room.Participants.Count < participantsToFind.Count)
                {
                    continue;
                }

                int missingCount = room.Participants.Where(p => !participantsToFind.Contains(p)).Count();
                if ((room.Participants.Count - missingCount) != participantsToFind.Count)
                {
                    continue;
                }

                toReturn.Add(room);
            }

            return toReturn;
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

        public async Task<IEditableRoom> OpenRoomAsync(IDictionary<string, string> attributes, CancellationToken token)
        {
            ThrowIfDisposed();

            UDPMulticastEditableRoom toReturn = new UDPMulticastEditableRoom(this, await SessionFactory.HostNewRoomAsync(token), multicastSettings.LocalIPAddress, attributes);
            locallyOwnedRooms.Add(toReturn);
            toReturn.StartHosting(DisposeCancellationToken);

            return toReturn;
        }
    }
}
