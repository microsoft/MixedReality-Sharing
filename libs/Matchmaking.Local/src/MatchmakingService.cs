// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Matchmaking.Local.Test")]

namespace Microsoft.MixedReality.Sharing.Matchmaking.Local
{
    /// <summary>
    /// Simple matchmaking service for local networks.
    ///
    /// Rooms are created and stored by the clients themselves. A room is open as long as its owner
    /// is connected and in the room. On room creation, the owner broadcasts a ROOM packet containing the room details.
    ///
    /// Clients who are looking for a room broadcast a FIND packet. Each owner replies with a ROOM
    /// packet for each room it owns.
    ///
    /// On the owner every room corresponds to a TCP port listening for connections. Other participants
    /// can join a room by making a connection to its port. The connection is then used to synchronize
    /// the room state and monitor the participants state (TODO).
    /// </summary>
    public class MatchmakingService : IMatchmakingService, IRoomManager, IDisposable
    {
        private static readonly byte[] roomHeader_ = new byte[] { (byte)'R', (byte)'O', (byte)'O', (byte)'M' };
        private static readonly byte[] findHeader_ = new byte[] { (byte)'F', (byte)'I', (byte)'N', (byte)'D' };

        private readonly List<OwnedRoom> ownedRooms_ = new List<OwnedRoom>();
        private readonly List<RoomBase> joinedRooms_ = new List<RoomBase>();
        private readonly MatchParticipantFactory participantFactory_;
        private readonly SocketerClient server_;
        private readonly SocketerClient broadcastSender_;
        private readonly BinaryFormatter formatter_ = new BinaryFormatter();

        public IEnumerable<IRoom> JoinedRooms => joinedRooms_;

        public IRoomManager RoomManager => this;

        public MatchmakingService(MatchParticipantFactory participantFactory, string broadcastAddress)
        {
            participantFactory_ = participantFactory;

            var localParticipant = participantFactory_.LocalParticipant;
            server_ = SocketerClient.CreateListener(SocketerClient.Protocol.UDP, localParticipant.Port, localParticipant.Host);
            server_.Message += OnMessage;
            server_.Start();
            broadcastSender_ = SocketerClient.CreateSender(SocketerClient.Protocol.UDP, broadcastAddress, localParticipant.Port, localParticipant.Host);
            broadcastSender_.Start();
        }

        public void Dispose()
        {
            server_.Stop();
            broadcastSender_.Stop();
        }

        private void OnMessage(SocketerClient server, SocketerClient.MessageEvent ev)
        {
            if (IsFindPacket(ev.Message))
            {
                // Reply with the rooms owned by the local participant.
                // TODO should just use one socket to send udp messages
                SocketerClient replySocket = SocketerClient.CreateSender(SocketerClient.Protocol.UDP, ev.SourceHost, server.Port);
                replySocket.Start();
                foreach (var room in ownedRooms_.Where(r => r.Visibility == RoomVisibility.Searchable))
                {
                    var packet = CreateRoomPacket(room);
                    replySocket.SendNetworkMessage(packet);
                }
                replySocket.Stop();
            }
        }

        public Task<IRoom> CreateRoomAsync(Dictionary<string, object> attributes = null, RoomVisibility visibility = RoomVisibility.NotVisible, CancellationToken token = default)
        {
            return Task.Run<IRoom>(() =>
            {
                // Make a new room.
                var localParticipant = participantFactory_.LocalParticipant;
                SocketerClient roomServer = SocketerClient.CreateListener(SocketerClient.Protocol.TCP, 0, localParticipant.Host);
                var newRoom = new OwnedRoom(this, roomServer, attributes, visibility, localParticipant);

                ownedRooms_.Add(newRoom);
                joinedRooms_.Add(newRoom);

                // Advertise it.
                if (visibility == RoomVisibility.Searchable)
                {
                    broadcastSender_.SendNetworkMessage(CreateRoomPacket(newRoom));
                }

                return Task.FromResult<IRoom>(newRoom);
            }, token);
        }

        private byte[] CreateRoomPacket(OwnedRoom room)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str, Encoding.UTF8))
            {
                // ROOM header
                writer.Write(roomHeader_);

                // GUID
                writer.Write(room.Guid.ToByteArray());

                // Attributes
                // Don't use .NET serialization for the whole map, wastes ~1KB per packet.
                writer.Write(room.Attributes.Count);
                foreach (var entry in room.Attributes)
                {
                    writer.Write(entry.Key);
                    var objStr = new MemoryStream();
                    formatter_.Serialize(objStr, entry.Value);
                    writer.Write(objStr.GetBuffer(), 0, (int)objStr.Length);
                }

                // Port
                writer.Write(room.Port);
            }
            return str.ToArray();
        }

        private RoomInfo ParseRoomPacket(string sender, byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip ROOM header
            str.Seek(roomHeader_.Length, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                // GUID
                var guidBytes = reader.ReadBytes(16);
                var id = new Guid(guidBytes);

                // Attributes
                var attrCount = reader.ReadInt32();
                var attributes = new Dictionary<string, object>(attrCount);
                for (int i = 0; i < attrCount; ++i)
                {
                    var key = reader.ReadString();
                    var objStr = new MemoryStream(packet, (int)str.Position, packet.Length - (int)str.Position);
                    object value = formatter_.Deserialize(objStr);
                    str.Seek(objStr.Position, SeekOrigin.Current);
                    attributes.Add(key, value);
                }

                // Room port.
                var port = reader.ReadUInt16();
                return new RoomInfo(this, id, sender, port, attributes, DateTime.UtcNow);
            }
        }

        private static bool IsRoomPacket(byte[] packet)
        {
            return packet.Take(roomHeader_.Length).SequenceEqual(roomHeader_);
        }
        private static bool IsFindPacket(byte[] packet)
        {
            return packet.Take(findHeader_.Length).SequenceEqual(findHeader_);
        }

        // Periodically sends FIND packets and collects results until it is disposed.
        // TODO should remove rooms after a timeout
        private class RoomList : IRoomList
        {
            // Interval between two FIND packets.
            private const int broadcastIntervalMs_ = 2000;

            private MatchmakingService service_;
            private List<RoomInfo> activeRooms_ = new List<RoomInfo>();
            private readonly CancellationTokenSource sendCts_ = new CancellationTokenSource();

            public event EventHandler<IEnumerable<IRoomInfo>> RoomsRefreshed;

            private byte[] CreateFindPacket()
            {
                // TODO should add find parameters
                return findHeader_;
            }

            public RoomList(MatchmakingService service)
            {
                service_ = service;
                service_.server_.Message += OnMessage;
                var token = sendCts_.Token;
                var findPacket = CreateFindPacket();

                // Start periodically sending FIND requests
                Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        service_.broadcastSender_.SendNetworkMessage(findPacket);
                        token.WaitHandle.WaitOne(broadcastIntervalMs_);
                    }
                }, token);
            }

            private void OnMessage(SocketerClient server, SocketerClient.MessageEvent ev)
            {
                //
                var service = service_;

                if (IsRoomPacket(ev.Message))
                {
                    // TODO shouldn't delay this thread but offload this to a queue
                    RoomInfo newRoom = service.ParseRoomPacket(ev.SourceHost, ev.Message);

                    List<RoomInfo> newRoomList = null;
                    lock (activeRooms_)
                    {
                        int index = activeRooms_.FindIndex(r => r.Id.Equals(newRoom.Id));
                        if (index >= 0)
                        {
                            // TODO check if equal
                            // TODO check timestamp
                            var oldRoom = activeRooms_[index];
                            activeRooms_[index] = newRoom;
                        }
                        else
                        {
                            activeRooms_.Add(newRoom);
                        }
                        newRoomList = new List<RoomInfo>(activeRooms_);
                    }
                    RoomsRefreshed?.Invoke(this, newRoomList);
                }
            }

            public void Dispose()
            {
                sendCts_.Cancel();
                service_.server_.Message -= OnMessage;
                service_ = null;
                activeRooms_ = null;
            }

            public IEnumerable<IRoomInfo> CurrentRooms
            {
                get
                {
                    return activeRooms_;
                }
            }
        }

        public IRoomList FindRoomsByAttributes(Dictionary<string, object> attributes = null)
        {
            // TODO should specify the correct params
            return new RoomList(this);
        }

        public IRoomList FindRoomsByOwner(IMatchParticipant owner)
        {
            // TODO should specify the correct params
            return new RoomList(this);
        }

        public IRoomList FindRoomsByParticipants(IEnumerable<IMatchParticipant> participants)
        {
            // TODO should specify the correct params
            return new RoomList(this);
        }

        public static void Shuffle<T>(IList<T> list, Random random)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public Task<IRoom> JoinRandomRoomAsync(Dictionary<string, object> expectedAttributes = null, CancellationToken token = default)
        {
            return Task<IRoom>.Run(async () =>
            {
                // TODO should specify the correct params
                using (var roomList = new RoomList(this))
                {
                    var random = new Random();

                    IList<RoomInfo> currentRooms = null;
                    var roomsUpdated = new AutoResetEvent(false);

                    EventHandler<IEnumerable<IRoomInfo>> handler = (object l, IEnumerable<IRoomInfo> rooms) =>
                    {
                        currentRooms = (IList<RoomInfo>)rooms;
                        roomsUpdated.Set();
                    };
                    roomList.RoomsRefreshed += handler;

                    while (true)
                    {
                        WaitHandle.WaitAny(new WaitHandle[] { roomsUpdated, token.WaitHandle });
                        token.ThrowIfCancellationRequested();

                        if (currentRooms.Any())
                        {
                            Shuffle(currentRooms, random);
                            foreach (var roomInfo in currentRooms)
                            {
                                var room = await roomInfo.JoinAsync(token);
                                if (room != null)
                                {
                                    return room;
                                }
                            }
                        }
                    }
                }
            }, token);
        }

        public Task<IRoom> JoinRoomByIdAsync(string roomId, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        internal Task<IRoom> JoinAsync(RoomInfo roomInfo, CancellationToken token)
        {
            return Task.Run<IRoom>(() =>
            {
                lock (joinedRooms_)
                {
                    if (joinedRooms_.Find(r => r.Guid == roomInfo.Guid) != null)
                    {
                        throw new InvalidOperationException("Room " + roomInfo.Guid + " is already joined");
                    }

                    // Make a socket.
                    SocketerClient socket = SocketerClient.CreateSender(SocketerClient.Protocol.TCP, roomInfo.Host, roomInfo.Port);

                    // Make a room.
                    var res = new ForeignRoom(roomInfo, participantFactory_.LocalParticipant, socket);

                    // Configure handlers and try to connect.
                    var ev = new ManualResetEventSlim();
                    Action<SocketerClient, int, string, int> connectHandler =
                    (SocketerClient server, int id, string clientHost, int clientPort) =>
                    {
                        // Connected; add the room to the joined list.
                        joinedRooms_.Add(res);
                        // Wake up the original task.
                        ev.Set();
                    };
                    socket.Connected += connectHandler;
                    socket.Disconnected += (SocketerClient server, int id, string clientHost, int clientPort) =>
                    {
                        joinedRooms_.Remove(res);
                        socket.Stop();
                    };
                    socket.Start();
                    ev.Wait(token);

                    // Now that the connection is established, we can return the room.
                    socket.Connected -= connectHandler;
                    return res;
                }
            }, token);
        }
    }
}
