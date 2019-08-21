using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Matchmaking.Local.Test
{
    public class LocalMatchmakingTest
    {
        static private IMatchmakingService MakeMatchmakingService(int userIndex)
        {
            //public Context(string broadcastAddress, ushort port, string localAddress)
            //Comms = new UDPComms(broadcastAddress, localAddress, port);
            //                Service = new SsdpMatchmakingService(Comms);
            return null;
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
            using (var svc1 = MakeMatchmakingService(1))
            {
                var room1 = svc1.CreateRoomAsync("Room1", "http://room1", null, cts.Token).Result;

                Assert.Equal("Room1", room1.Id);
                Assert.Equal("http://room1", room1.Connection);
                Assert.Empty(room1.Attributes);

                var attributes = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 2 };
                var room2 = svc1.CreateRoomAsync("Room2", "foo://room2", attributes, cts.Token).Result;

                Assert.Equal("Room2", room2.Id);
                Assert.Equal("foo://room2", room2.Connection);
                Assert.Equal(1, room2.Attributes["prop1"]);
                Assert.Equal(2, room2.Attributes["prop2"]);

                Assert.NotEqual(room1.Id, room2.Id);
            }
        }

        private void AssertSame(IRoom lhs, IRoom rhs)
        {
            // ID is equal.
            Assert.Equal(lhs.Id, rhs.Id);
            Assert.Equal(lhs.Connection, rhs.Connection);

            // Attributes are equal.
            var lAttributes = lhs.Attributes.OrderBy(a => a.Key);
            var rAttributes = rhs.Attributes.OrderBy(a => a.Key);
            Assert.True(lAttributes.SequenceEqual(rAttributes));
        }

        private bool SameRoom(IRoom lhs, IRoom rhs)
        {
            if (lhs.Id != rhs.Id) return false;
            if (lhs.Connection != rhs.Connection) return false;

            // Attributes are equal.
            var lAttributes = lhs.Attributes.OrderBy(a => a.Key);
            var rAttributes = rhs.Attributes.OrderBy(a => a.Key);
            return lAttributes.SequenceEqual(rAttributes);
        }

        class RaiiGuard : IDisposable
        {
            private Action Quit { get; set; }
            public RaiiGuard(Action init, Action quit)
            {
                Quit = quit;
                if (init != null) init();
            }
            void IDisposable.Dispose()
            {
                if (Quit != null) Quit();
            }
        }

        // Return true if the collection satisfies 'pred' before 'token' is cancelled.
        // Otherwise return false.
        private bool WaitForCollectionPredicate<T>(ReadOnlyObservableCollection<T> coll, Func<bool> pred, CancellationToken token)
        {
            bool predicateResult = pred();
            if (predicateResult)
            {
                return true; // optimistic path
            }
            using (var wakeUp = new AutoResetEvent(false))
            {
                var ncoll = ((INotifyCollectionChanged)coll); //https://stackoverflow.com/questions/2058176
                NotifyCollectionChangedEventHandler onChange = (object sender, NotifyCollectionChangedEventArgs e) => wakeUp.Set();

                using (var unregisterCancel = token.Register(() => wakeUp.Set()))
                using (var unregisterWatch = new RaiiGuard(() => ncoll.CollectionChanged += onChange, () => ncoll.CollectionChanged -= onChange))
                {
                    while (true)
                    {
                        predicateResult = pred();
                        if (predicateResult)
                        {
                            return true;
                        }
                        wakeUp.WaitOne();
                        if (token.IsCancellationRequested)
                        {
                            return false;
                        }
                    }
                }
            }
        }
        

#if false
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
#endif
        [Fact]
        public void FindRoomByAttribute()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = MakeMatchmakingService(1))
            using (var svc2 = MakeMatchmakingService(2))
            {
                var attributes1 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 1 };
                var attributes2 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 123 };
                // Create some rooms in the first one
                var room1 = svc1.CreateRoomAsync("Room1", "Conn1", attributes1, cts.Token).Result;
                var room2 = svc1.CreateRoomAsync("Room2", "Conn2", attributes2, cts.Token).Result;
                var room3 = svc1.CreateRoomAsync("Room3", "Conn3", attributes2, cts.Token).Result;

                // Discover them from the second
                {
                    var rooms = svc2.StartDiscovery(null);
                    WaitForCollectionPredicate(rooms, () => rooms.Count >= 3, cts.Token);
                    svc2.StopDiscovery(rooms);
                    Assert.Equal(3, rooms.Count());
                    Assert.Contains(rooms, r => r.Id.Equals(room1.Id));
                    Assert.Contains(rooms, r => r.Id.Equals(room2.Id));
                    Assert.Contains(rooms, r => r.Id.Equals(room3.Id));
                }

                {
                    var rooms = svc2.StartDiscovery(new Dictionary<string, object> { ["prop2"] = 123 });
                    WaitForCollectionPredicate(rooms, () => rooms.Any(), cts.Token);
                    svc2.StopDiscovery(rooms);
                    Assert.Contains(rooms, r => SameRoom(r, room2));
                    Assert.Contains(rooms, r => SameRoom(r, room3));
                }
            }
        }

        [Fact]
        public void JoinRandomRoom()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = MakeMatchmakingService(1))
            using (var svc2 = MakeMatchmakingService(2))
            {
                var attributes1 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 1 };
                var attributes2 = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 123 };
                var room1 = svc1.CreateRoomAsync("Room1", "foo1", attributes1, cts.Token).Result;
                var room2 = svc2.CreateRoomAsync("Room2", "foo2", attributes2, cts.Token).Result;

                {
                    var rooms = svc2.StartDiscovery(null);
                    WaitForCollectionPredicate(rooms, () => rooms.Count > 0, cts.Token);
                    svc2.StopDiscovery(rooms);
                    var joinedRoom = rooms.First();
                    Assert.True(joinedRoom.Id.Equals(room1.Id) || joinedRoom.Id.Equals(room2.Id));
                    var roomToCompare = joinedRoom.Id.Equals(room1.Id) ? room1 : room2;
                    AssertSame(joinedRoom, roomToCompare);
                }

                {
                    var req2 = new Dictionary<string, object> { ["prop2"] = 123 };
                    var rooms = svc2.StartDiscovery(req2);
                    WaitForCollectionPredicate(rooms, () => rooms.Count > 0, cts.Token);
                    svc2.StopDiscovery(rooms);
                    Assert.Single(rooms);
                    AssertSame(rooms.First(), room2);
                }
            }
        }

        [Fact]
        public void JoinRoomById()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = MakeMatchmakingService(1))
            using (var svc2 = MakeMatchmakingService(2))
            {
                // Create rooms from service1
                var attributes = new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 2 };
                var room1 = svc1.CreateRoomAsync("Room1", "conn1", attributes, cts.Token).Result;
                var room2 = svc1.CreateRoomAsync("Room2", "conn2", attributes, cts.Token).Result;

                {
                    var req1 = new Dictionary<string, object> { ["id"] = room1.Id };
                    var rooms = svc2.StartDiscovery(req1);
                    WaitForCollectionPredicate(rooms, () => rooms.Count > 0, cts.Token);
                    svc2.StopDiscovery(rooms);
                    Assert.Single(rooms);
                    AssertSame(rooms.First(), room1);
                }

                {
                    var req2 = new Dictionary<string, object> { ["id"] = room2.Id };
                    var rooms = svc2.StartDiscovery(req2);
                    WaitForCollectionPredicate(rooms, () => rooms.Count > 0, cts.Token);
                    svc2.StopDiscovery(rooms);
                    Assert.Single(rooms);
                    AssertSame(rooms.First(), room2);
                }
            }
        }

        [Fact]
        public void Mix()
        {
            using (var svc1 = MakeMatchmakingService(1))
            using (var svc2 = MakeMatchmakingService(2))
            using (var svc3 = MakeMatchmakingService(3))
            {
                var room1 = svc1.CreateRoomAsync(
                    "MixRoom", "MixRoomConn",
                    new Dictionary<string, object> { ["prop1"] = 1, ["prop2"] = 2 }
                    ).Result;

#if false
                IRoom foundRoom = null;
                {
                    var rooms = svc2.StartDiscovery(null);
                    var ev = new AutoResetEvent(false);
                    roomList.ListUpdated += (object sender, IRoomList updated) =>
                    {
                        var list = updated.Rooms;
                        Assert.Single(list);
                        foundRoom = list.ElementAt(0);
                        Assert.Equal(foundRoom.Id, room1.Id);
                        ev.Set();
                    };
                    ev.WaitOne(TestTimeoutMs);
                }
                Assert.NotNull(foundRoom);
                Assert.Equal(room1.Id, foundRoom.Id);
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
#endif
            }
        }
    }
}
