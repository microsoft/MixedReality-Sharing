// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.StateSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    /// </summary>
    public class LocalMatchmakingService : IMatchmakingService, IRoomManager, IDisposable
    {
        private static readonly byte[] roomHeader_ = new byte[] { (byte)'R', (byte)'O', (byte)'O', (byte)'M' };
        private static readonly byte[] findHeader_ = new byte[] { (byte)'F', (byte)'I', (byte)'N', (byte)'D' };

        private readonly List<LocalRoom> joinedRooms_ = new List<LocalRoom>();
        private readonly LocalMatchParticipantFactory participantFactory_;
        private readonly SocketerClient server_;
        private readonly SocketerClient broadcastSender_;
        private readonly BinaryFormatter formatter_ = new BinaryFormatter();

        public IEnumerable<IRoom> JoinedRooms => joinedRooms_;

        public IRoomManager RoomManager => this;

        public LocalMatchmakingService(LocalMatchParticipantFactory participantFactory, string broadcastAddress)
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
                var localId = participantFactory_.LocalParticipantId;
                foreach (var room in joinedRooms_.Where(r => r.Owner.Id.Equals(localId) && r.Visibility == RoomVisibility.Searchable))
                {
                    var packet = CreateRoomPacket(room);
                    replySocket.SendNetworkMessage(packet);
                }
                replySocket.Stop();
            }
        }

        public Task<IRoom> CreateRoomAsync(Dictionary<string, object> attributes = null, RoomVisibility visibility = RoomVisibility.NotVisible, CancellationToken token = default)
        {
            // Make a new room.
            var localParticipant = participantFactory_.LocalParticipant;
            var newRoom = new LocalRoom(Guid.NewGuid().ToString(), visibility, attributes, localParticipant);
            joinedRooms_.Add(newRoom);

            // Advertise it.
            if (visibility == RoomVisibility.Searchable)
            {
                broadcastSender_.SendNetworkMessage(CreateRoomPacket(newRoom));
            }

            return Task.FromResult<IRoom>(newRoom);
        }

        private byte[] CreateRoomPacket(LocalRoomInfo roomInfo)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str, Encoding.UTF8))
            {
                // ROOM header
                writer.Write(roomHeader_);

                // GUID
                writer.Write(Guid.Parse(roomInfo.Id).ToByteArray());

                // Attributes
                // Don't use .NET serialization for the whole map, wastes ~1KB per packet.
                writer.Write(roomInfo.Attributes.Count);
                foreach (var entry in roomInfo.Attributes)
                {
                    writer.Write(entry.Key);
                    var objStr = new MemoryStream();
                    formatter_.Serialize(objStr, entry.Value);
                    writer.Write(objStr.GetBuffer(), 0, (int)objStr.Length);
                }
                // TODO owner, connection string to room
            }
            return str.ToArray();
        }

        private LocalRoomInfo ParseRoomPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip ROOM header
            str.Seek(roomHeader_.Length, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                // GUID
                var guidBytes = reader.ReadBytes(16);
                var id = new Guid(guidBytes).ToString();

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

                // TODO owner, connection string to room
                return new LocalRoomInfo(id, attributes);
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

            private LocalMatchmakingService service_;
            private List<LocalRoomInfo> activeRooms_ = new List<LocalRoomInfo>();
            private readonly CancellationTokenSource sendCts_ = new CancellationTokenSource();

            public event EventHandler<IEnumerable<IRoomInfo>> RoomsRefreshed;

            private byte[] CreateFindPacket()
            {
                // TODO should add find parameters
                return findHeader_;
            }

            public RoomList(LocalMatchmakingService service)
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
                    LocalRoomInfo newRoom = service.ParseRoomPacket(ev.Message);

                    List<LocalRoomInfo> newRoomList = null;
                    lock(activeRooms_)
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
                        newRoomList = new List<LocalRoomInfo>(activeRooms_);
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
            return Task<IRoom>.Run(async() =>
            {
                // TODO should specify the correct params
                using (var roomList = new RoomList(this))
                {
                    var random = new Random();

                    IList<LocalRoomInfo> currentRooms = null;
                    var roomsUpdated = new AutoResetEvent(false);

                    EventHandler<IEnumerable<IRoomInfo>> handler = (object l, IEnumerable<IRoomInfo> rooms) =>
                    {
                        currentRooms = (IList<LocalRoomInfo>)rooms;
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
    }

    class LocalRoomInfo : IRoomInfo
    {
        public string Id { get; }

        public Dictionary<string, object> Attributes { get; } = new Dictionary<string, object>();

        internal DateTime LastHeard;

        // TODO connection information

        public LocalRoomInfo(string id, Dictionary<string, object> attributes)
        {
            LastHeard = DateTime.UtcNow;
            Id = id;

            if (attributes != null)
            {
                foreach(var attr in attributes)
                {
                    Attributes.Add(attr.Key, attr.Value);
                }
            }
        }

        public Task<IRoom> JoinAsync(CancellationToken token = default)
        {
            // TODO
            return Task.FromResult<IRoom>(new LocalRoom(Id, RoomVisibility.NotVisible, null, null));
        }
    }


    class LocalRoom : LocalRoomInfo, IRoom
    {
        public IMatchParticipant Owner { get; }

        public IEnumerable<IMatchParticipant> Participants { get; } = new List<IMatchParticipant>();

        public IStateSubscription State => throw new NotImplementedException();

        public RoomVisibility Visibility { get; internal set; }

        public event EventHandler AttributesChanged;

        public LocalRoom(string id, RoomVisibility visibility, Dictionary<string, object> attributes, LocalMatchParticipant owner) : base(id, attributes)
        {
            Visibility = visibility;
            Owner = owner;
        }

        public Task LeaveAsync()
        {
            throw new NotImplementedException();
        }

        public Task SetAttributesAsync(Dictionary<string, object> attributes)
        {
            throw new NotImplementedException();
        }

        public Task SetVisibility(RoomVisibility val)
        {
            Visibility = val;
            return Task.CompletedTask;
        }
    }
}
