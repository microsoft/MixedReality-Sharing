// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Matchmaking.Local.Test
{
    public abstract class LocalMatchmakingTest
    {
        Func<int, IMatchmakingService> matchmakingServiceFactory_;

        protected LocalMatchmakingTest(Func<int, IMatchmakingService> matchmakingServiceFactory)
        {
            matchmakingServiceFactory_ = matchmakingServiceFactory;
        }

        private static int TestTimeoutMs
        {
            get
            {
                return Debugger.IsAttached ? Timeout.Infinite : 10000;
            }
        }

        private static void AssertSameDictionary(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        {
            Assert.Equal(a.Count, b.Count);
            foreach (var entry in a)
            {
                Assert.Equal(entry.Value, b[entry.Key]);
            }
        }

        [Fact]
        public void CreateRoom()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            {
                var room1 = svc1.CreateRoomAsync("CreateRoom", "http://room1", null, cts.Token).Result;

                Assert.Equal("http://room1", room1.Connection);
                Assert.Empty(room1.Attributes);

                var attributes = new Dictionary<string, string> { ["prop1"] = "1", ["prop2"] = "2" };
                var room2 = svc1.CreateRoomAsync("CreateRoom", "foo://room2", attributes, cts.Token).Result;

                Assert.Equal("foo://room2", room2.Connection);
                Assert.Equal("1", room2.Attributes["prop1"]);
                Assert.Equal("2", room2.Attributes["prop2"]);
            }
        }

        private void AssertSame(IRoom lhs, IRoom rhs)
        {
            // ID is equal.
            Assert.Equal(lhs.Connection, rhs.Connection);

            // Attributes are equal.
            //var lAttributes = lhs.Attributes.OrderBy(a => a.Key);
            //var rAttributes = rhs.Attributes.OrderBy(a => a.Key);
            //Assert.True(lAttributes.SequenceEqual(rAttributes));
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

        // Run a query and wait for the predicate to be satisfied.
        // Return the list of rooms which satisfied the predicate or null if cancelled before the preducate was satisfied.
        private IEnumerable<IRoom> QueryAndWaitForRoomsPredicate(
            IMatchmakingService svc, string type,
            Func<IEnumerable<IRoom>, bool> pred, CancellationToken token)
        {
            using (var result = svc.StartDiscovery(type))
            {
                var rooms = result.Rooms;
                bool predicateResult = pred(rooms);
                if (predicateResult)
                {
                    return rooms; // optimistic path
                }
                using (var wakeUp = new AutoResetEvent(false))
                {
                    Action<IDiscoveryTask> onChange = (IDiscoveryTask sender) => wakeUp.Set();

                    using (var unregisterCancel = token.Register(() => wakeUp.Set()))
                    using (var unregisterWatch = new RaiiGuard(() => result.Updated += onChange, () => result.Updated -= onChange))
                    {
                        while (true)
                        {
                            rooms = result.Rooms;
                            if (pred(rooms))
                            {
                                return rooms;
                            }
                            wakeUp.WaitOne(); // wait for cancel or update
                            if (token.IsCancellationRequested)
                            {
                                return null;
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void FindRoomsLocalAndRemote()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var svc2 = matchmakingServiceFactory_(2))
            {
                // Create some rooms in the first one
                const string category = "FindRoomsLocalAndRemote";
                var room1 = svc1.CreateRoomAsync(category, "Conn1", null, cts.Token).Result;
                var room2 = svc1.CreateRoomAsync(category, "Conn2", null, cts.Token).Result;
                var room3 = svc1.CreateRoomAsync(category, "Conn3", null, cts.Token).Result;

                // Discover them from the first service
                {
                    var rooms = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Count() >= 3, cts.Token);
                    Assert.Equal(3, rooms.Count());
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room1.UniqueId));
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room2.UniqueId));
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room3.UniqueId));
                }

                // And also from the second
                {
                    var rooms = QueryAndWaitForRoomsPredicate(svc2, category, rl => rl.Count() >= 3, cts.Token);
                    Assert.Equal(3, rooms.Count());
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room1.UniqueId));
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room2.UniqueId));
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room3.UniqueId));
                }
            }
        }

        [Fact]
        public void FindRoomsFromAnnouncement()
        {
            // start discovery, then start services afterwards

            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var svc2 = matchmakingServiceFactory_(2))
            {
                const string category = "FindRoomsFromAnnouncement";

                using (var task1 = svc1.StartDiscovery(category))
                using (var task2 = svc2.StartDiscovery(category))
                {
                    Assert.Empty(task1.Rooms);
                    Assert.Empty(task2.Rooms);

                    var room1 = svc1.CreateRoomAsync(category, "foo1", null, cts.Token).Result;

                    // local
                    var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(res1.First().UniqueId, room1.UniqueId);
                    // remote
                    var res2 = QueryAndWaitForRoomsPredicate(svc2, category, rl => rl.Any(), cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(res1.First().UniqueId, room1.UniqueId);
                }
            }
        }

        [Fact]
        public void ServiceShutdownRemovesRooms()
        {
            // start discovery, then start services afterwards

            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            {
                const string category = "ServiceShutdownRemovesRooms";
                var rooms1 = svc1.StartDiscovery(category);
                Assert.Empty(rooms1.Rooms);

                using (var svc2 = matchmakingServiceFactory_(2))
                {
                    // Create rooms from svc2
                    var room1 = svc2.CreateRoomAsync(category, "conn1", null, cts.Token).Result;

                    // It should show up in svc1
                    {
                        var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        Assert.Equal(room1.UniqueId, res1.First().UniqueId);
                    }
                }

                // After svc2 is shut down, its rooms should be gone from svc1
                {
                    var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Count() == 0, cts.Token);
                    Assert.Empty(res1);
                }
            }
        }

        [Fact]
        public void CanEditRoomAttributes()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            {
                const string category = "CanEditRoomAttributes";
                var rooms1 = svc1.StartDiscovery(category);
                Assert.Empty(rooms1.Rooms);

                using (var svc2 = matchmakingServiceFactory_(2))
                {
                    // Create rooms from svc2
                    var origAttrs = new Dictionary<string, string> { { "keyA", "valA" }, { "keyB", "valB" } };
                    var room2 = svc2.CreateRoomAsync(category, "conn1", origAttrs, cts.Token).Result;

                    // It should show up in svc1
                    {
                        var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        var room1 = res1.First();
                        Assert.Equal(room2.UniqueId, room1.UniqueId);
                        AssertSameDictionary(res1.First().Attributes, origAttrs);
                        Assert.Null(room1.RequestEdit()); // remote edit not yet supported
                    }

                    // Commit edits in service2
                    {
                        var edit2 = room2.RequestEdit();
                        Assert.NotNull(edit2);
                        edit2.RemoveAttribute("keyA");
                        edit2.PutAttribute("keyB", "updatedB");
                        var task = edit2.CommitAsync();
                        task.Wait(cts.Token);
                        Assert.Equal(TaskStatus.RanToCompletion, task.Status);
                    }

                    {
                        var newAttrs = new Dictionary<string, string> { { "keyB", "updatedB" } };

                        // Edits should show locally
                        AssertSameDictionary(room2.Attributes, newAttrs);

                        // And remotely. This currently waits for the attributes to change. It would be nice if we could inspect the network instead.
                        var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any() && rl.First().Attributes.Count == 1, cts.Token);
                        Assert.Single(res1);
                        var room1 = res1.First();
                        Assert.Equal(room2.UniqueId, room1.UniqueId);
                        AssertSameDictionary(res1.First().Attributes, newAttrs);
                    }
                }
            }
        }

#if false
        [Fact]
        public void RoomExpiresOnTime()
        {
            using (var cts = new CancellationTokenSource(TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            {
                const string category = "RoomExpiresOnTime";
                var rooms1 = svc1.StartDiscovery(category);
                Assert.Empty(rooms1.Rooms);

                using (var svc2 = matchmakingServiceFactory_(2))
                {
                    // Create rooms from svc2
                    // SET TIMEOUT 2s
                    var room1 = svc2.CreateRoomAsync(category, "conn1",  null, cts.Token).Result;

                    // It should show up in svc1
                    {
                        var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        Assert.Equal(room1.UniqueId, res1.First().UniqueId);
                    }

                    // how to stop svc2 from announcing without bye

                    // 
                    {
                        var res1 = QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Count() == 0, cts.Token);
                        Assert.Single(res1);
                        Assert.Equal(room1.UniqueId, res1.First().UniqueId);
                    }
                }
            }
        }
#endif
    }

    public class LocalMatchmakingTestUdp : LocalMatchmakingTest
    {
        static private IMatchmakingService MakeMatchmakingService(int userIndex)
        {
            var net = new UdpPeerNetwork(new IPAddress(0xffffff7f), 45277, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerMatchmakingService(net);
        }

        public LocalMatchmakingTestUdp() : base(MakeMatchmakingService) { }
    }

    public class LocalMatchmakingTestUdpMulticast : LocalMatchmakingTest
    {
        static private IMatchmakingService MakeMatchmakingService(int userIndex)
        {
            var net = new UdpPeerNetwork(new IPAddress(0x000000e0), 45278, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerMatchmakingService(net);
        }

        public LocalMatchmakingTestUdpMulticast() : base(MakeMatchmakingService) { }
    }

    public class LocalMatchmakingTestMemory : LocalMatchmakingTest
    {
        static private IMatchmakingService MakeMatchmakingService(int userIndex)
        {
            var net = new MemoryPeerNetwork(userIndex);
            return new PeerMatchmakingService(net);
        }

        public LocalMatchmakingTestMemory() : base(MakeMatchmakingService) { }
    }
}