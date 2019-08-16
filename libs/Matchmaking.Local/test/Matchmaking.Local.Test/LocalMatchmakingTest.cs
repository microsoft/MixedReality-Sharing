using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Matchmaking.Local;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Xunit;

namespace Matchmaking.Local.Test
{
    public class LocalMatchmakingTest
    {
        private class Context : IDisposable
        {
            public MatchParticipantFactory PFactory;
            public MatchmakingService Service;

            public Context(string id, string name, string broadcastAddress, ushort port, string localAddress)
            {
                PFactory = new MatchParticipantFactory(id, name);
                Service = new MatchmakingService(PFactory, broadcastAddress, port, localAddress);
            }

            public void Dispose()
            {
                Service.Dispose();
            }
        }

        private static int TestTimeoutMs
        {
            get
            {
                return Debugger.IsAttached ? Timeout.Infinite : 10000;
            }
        }

        private static void AssertSameAttributes(IRoom a, IRoom b)
        {
            Assert.Equal(a.Attributes.Count, b.Attributes.Count);
            foreach (var entry in a.Attributes)
            {
                Assert.Equal(entry.Value, b.Attributes[entry.Key]);
            }
        }

        [Fact]
        public void CreateRoom()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var ctx1 = new Context("1", "Participant 1", "127.255.255.255", 45678, "127.0.0.1"))
            {
                var room1 = ctx1.Service.CreateRoomAsync(null, RoomVisibility.NotVisible, cts.Token).Result;

                Assert.Single(ctx1.Service.JoinedRooms);
                Assert.Equal(ctx1.Service.JoinedRooms.First(), room1);

                Assert.Single(room1.Participants);
                Assert.Equal(ctx1.PFactory.LocalParticipantId, room1.Participants.First().MatchParticipant.Id);
                Assert.Empty(room1.Attributes);

                var attributes = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 2 };
                var room2 = ctx1.Service.CreateRoomAsync(attributes, RoomVisibility.Searchable, cts.Token).Result;

                Assert.Equal(2, ctx1.Service.JoinedRooms.Count());
                Assert.Contains(room1, ctx1.Service.JoinedRooms);
                Assert.Contains(room2, ctx1.Service.JoinedRooms);

                Assert.Single(room2.Participants);
                Assert.Equal(ctx1.PFactory.LocalParticipantId, room2.Participants.First().MatchParticipant.Id);
                Assert.Equal(1, room2.Attributes["prop1"]);
                Assert.Equal(2, room2.Attributes["prop2"]);

                Assert.NotEqual(room1.Id, room2.Id);
            }
        }

        private void AssertSame(IRoomInfo lhs, IRoomInfo rhs)
        {
            // ID is equal.
            Assert.Equal(lhs.Id, rhs.Id);

            // Attributes are equal.
            var lAttributes = lhs.Attributes.OrderBy(a => a.Key);
            var rAttributes = rhs.Attributes.OrderBy(a => a.Key);
            Assert.True(lAttributes.SequenceEqual(rAttributes));
        }

        private void AssertSame(IRoom lhs, IRoom rhs)
        {
            AssertSame(lhs, (IRoomInfo)rhs);

            var lParticipants = lhs.Participants.OrderBy(p => p.IdInRoom);
            var rParticipants = rhs.Participants.OrderBy(p => p.IdInRoom);
            // Participant IDs in room are equal.
            Assert.True(lParticipants.Select(p => p.IdInRoom).SequenceEqual(rParticipants.Select(p => p.IdInRoom)));
            // Match participant IDs are equal.
            Assert.True(lParticipants.Select(p => p.MatchParticipant.Id).SequenceEqual(rParticipants.Select(p => p.MatchParticipant.Id)));
        }

        [Fact]
        public void FindRoomByAttribute()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var ctx1 = new Context("1", "Participant 1", "127.255.255.255", 45678, "127.0.0.1"))
            using (var ctx2 = new Context("2", "Participant 2", "127.255.255.255", 45678, "127.0.0.2"))
            {
                var attributes1 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 1 };
                var attributes2 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 123 };
                var room1 = ctx1.Service.CreateRoomAsync(attributes1, RoomVisibility.Searchable, cts.Token).Result;
                var room2 = ctx1.Service.CreateRoomAsync(attributes2, RoomVisibility.Searchable, cts.Token).Result;
                var room3 = ctx1.Service.CreateRoomAsync(attributes2, RoomVisibility.ByParticipantOnly, cts.Token).Result;

                using (var roomList = ctx2.Service.FindRoomsByAttributes())
                {
                    var rooms = roomList.CurrentRooms;
                    while (rooms.Count() < 2)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        rooms = roomList.CurrentRooms;
                    }
                    Assert.Equal(2, rooms.Count());
                    Assert.Contains(rooms, r => r.Id.Equals(room1.Id));
                    Assert.Contains(rooms, r => r.Id.Equals(room2.Id));
                }

                using (var roomList = ctx2.Service.FindRoomsByAttributes(new Dictionary<string, object> { ["prop2"] = 123 }))
                {
                    var rooms = roomList.CurrentRooms;
                    while (!rooms.Any())
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        rooms = roomList.CurrentRooms;
                    }
                    Assert.Single(rooms);
                    AssertSame(rooms.First(), room2);
                }
            }
        }

        [Fact]
        public void JoinRandomRoom()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var ctx1 = new Context("1", "Participant 1", "127.255.255.255", 45678, "127.0.0.1"))
            using (var ctx2 = new Context("2", "Participant 2", "127.255.255.255", 45678, "127.0.0.2"))
            {
                var attributes1 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 1 };
                var attributes2 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 123 };
                var room1 = ctx1.Service.CreateRoomAsync(attributes1, RoomVisibility.Searchable, cts.Token).Result;
                var room2 = ctx1.Service.CreateRoomAsync(attributes2, RoomVisibility.Searchable, cts.Token).Result;

                var joinedRoom = ctx2.Service.JoinRandomRoomAsync(null, cts.Token).Result;
                Assert.True(joinedRoom.Id.Equals(room1.Id) || joinedRoom.Id.Equals(room2.Id));
                var roomToCompare = joinedRoom.Id.Equals(room1.Id) ? room1 : room2;
                AssertSame(joinedRoom, roomToCompare);

                var joinedRoom2 = ctx2.Service.JoinRandomRoomAsync(new Dictionary<string, object> { ["prop2"] = 123 }, cts.Token).Result;
                AssertSame(joinedRoom2, room2);
            }
        }

        [Fact]
        public void JoinRoomById()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var ctx1 = new Context("1", "Participant 1", "127.255.255.255", 45678, "127.0.0.1"))
            using (var ctx2 = new Context("2", "Participant 2", "127.255.255.255", 45678, "127.0.0.2"))
            {
                var attributes = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 2 };
                var room1 = ctx1.Service.CreateRoomAsync(attributes, RoomVisibility.Searchable, cts.Token).Result;
                var room2 = ctx1.Service.CreateRoomAsync(attributes, RoomVisibility.NotVisible, cts.Token).Result;

                var joinedRoom1 = ctx2.Service.JoinRoomByIdAsync(room1.Id, cts.Token).Result;
                AssertSame(joinedRoom1, room1);

                var joinedRoom2 = ctx2.Service.JoinRoomByIdAsync(room2.Id, cts.Token).Result;
                AssertSame(joinedRoom2, room2);
            }
        }

        [Fact]
        public void Mix()
        {
            using (var ctx1 = new Context("1", "Participant 1", "127.255.255.255", 45678, "127.0.0.1"))
            using (var ctx2 = new Context("2", "Participant 2", "127.255.255.255", 45678, "127.0.0.2"))
            using (var ctx3 = new Context("3", "Participant 3", "127.255.255.255", 45678, "127.0.0.3"))
            {
                var room1 = ctx1.Service.CreateRoomAsync(
                    new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 2},
                    RoomVisibility.Searchable).Result;
                Assert.Single(room1.Participants);

                var hiddenRoom = ctx1.Service.CreateRoomAsync(null).Result;
                IRoomInfo foundRoom = null;
                using (var roomList = ctx2.Service.FindRoomsByAttributes())
                {
                    var ev = new AutoResetEvent(false);
                    roomList.RoomsRefreshed += (object o, IEnumerable<IRoomInfo> list) =>
                    {
                        Assert.Single(list);
                        foundRoom = list.ElementAt(0);
                        Assert.Equal(foundRoom.Id, room1.Id);
                        ev.Set();
                    };
                    ev.WaitOne(TestTimeoutMs);
                }
                Assert.NotNull(foundRoom);

                var room2 = (RoomBase)foundRoom.JoinAsync().Result;
                Assert.Equal(room1.Id, room2.Id);

                {
                    var cts = new CancellationTokenSource(TestTimeoutMs);
                    while (room2.Attributes.Count != room1.Attributes.Count)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                    }
                }
                Assert.Equal(room1.Attributes, room2.Attributes);


                room2.SetAttributesAsync(new Dictionary<string, object> { ["prop1"] = 42 }).Wait();
                Assert.Equal(42, room2.Attributes["prop1"]);
                {
                    var cts = new CancellationTokenSource(TestTimeoutMs);
                    while (!room1.Attributes["prop1"].Equals(42))
                    {
                        cts.Token.ThrowIfCancellationRequested();
                    }
                }
                Assert.Equal(2, room1.Participants.Count());
                Assert.Equal(2, room2.Participants.Count());

                var room3 = (RoomBase)ctx3.Service.JoinRandomRoomAsync().Result;
                Assert.Equal(room1.Id, room3.Id);
                {
                    var cts = new CancellationTokenSource(TestTimeoutMs);
                    while (room3.Attributes.Count != 2)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                    }
                }

                AssertSameAttributes(room1, room3);
                Assert.Equal(3, room1.Participants.Count());
                Assert.Equal(3, room2.Participants.Count());
                Assert.Equal(3, room3.Participants.Count());

                room2.SendMessage(room2.Participants.First(p => p.MatchParticipant != null && p.MatchParticipant.Id.Equals(ctx3.PFactory.LocalParticipantId)), Encoding.UTF8.GetBytes("hello"));
                {
                    var ev = new ManualResetEventSlim();
                    room3.MessageReceived += (object o, MessageReceivedArgs args) =>
                    {
                        Assert.Equal(ctx2.PFactory.LocalParticipantId, args.Sender.MatchParticipant.Id);
                        Assert.Equal("hello", Encoding.UTF8.GetString(args.Payload));
                        ev.Set();
                    };

                    var cts = new CancellationTokenSource(TestTimeoutMs);
                    ev.Wait(cts.Token);
                }
            }
        }
    }
}
