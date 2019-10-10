// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Matchmaking.Local.Test
{
    public abstract class PeerDiscoveryTest
    {
        protected Func<int, IDiscoveryAgent> matchmakingServiceFactory_;

        protected PeerDiscoveryTest(Func<int, IDiscoveryAgent> matchmakingServiceFactory)
        {
            matchmakingServiceFactory_ = matchmakingServiceFactory;
        }

        [Fact]
        public void CreateRoom()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            {
                var room1 = svc1.PublishAsync("CreateRoom", "http://room1", null, cts.Token).Result;

                Assert.Equal("http://room1", room1.Connection);
                Assert.Empty(room1.Attributes);

                var attributes = new Dictionary<string, string> { ["prop1"] = "1", ["prop2"] = "2" };
                var room2 = svc1.PublishAsync("CreateRoom", "foo://room2", attributes, cts.Token).Result;

                Assert.Equal("foo://room2", room2.Connection);
                Assert.Equal("1", room2.Attributes["prop1"]);
                Assert.Equal("2", room2.Attributes["prop2"]);
            }
        }

        [Fact]
        public void FindRoomsLocalAndRemote()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var svc2 = matchmakingServiceFactory_(2))
            {
                // Create some rooms in the first one
                const string category = "FindRoomsLocalAndRemote";
                var room1 = svc1.PublishAsync(category, "Conn1", null, cts.Token).Result;
                var room2 = svc1.PublishAsync(category, "Conn2", null, cts.Token).Result;
                var room3 = svc1.PublishAsync(category, "Conn3", null, cts.Token).Result;

                // Discover them from the first service
                {
                    var rooms = Utils.QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Count() >= 3, cts.Token);
                    Assert.Equal(3, rooms.Count());
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room1.UniqueId));
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room2.UniqueId));
                    Assert.Contains(rooms, r => r.UniqueId.Equals(room3.UniqueId));
                }

                // And also from the second
                {
                    var rooms = Utils.QueryAndWaitForRoomsPredicate(svc2, category, rl => rl.Count() >= 3, cts.Token);
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

            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var svc2 = matchmakingServiceFactory_(2))
            {
                const string category = "FindRoomsFromAnnouncement";

                using (var task1 = svc1.Subscribe(category))
                using (var task2 = svc2.Subscribe(category))
                {
                    Assert.Empty(task1.Rooms);
                    Assert.Empty(task2.Rooms);

                    var room1 = svc1.PublishAsync(category, "foo1", null, cts.Token).Result;

                    // local
                    var res1 = Utils.QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(res1.First().UniqueId, room1.UniqueId);
                    // remote
                    var res2 = Utils.QueryAndWaitForRoomsPredicate(svc2, category, rl => rl.Any(), cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(res1.First().UniqueId, room1.UniqueId);
                }
            }
        }

        [Fact]
        public void ServiceShutdownRemovesRooms()
        {
            const string category1 = "ServiceShutdownRemovesRooms1";
            const string category2 = "ServiceShutdownRemovesRooms2";

            // start discovery, then start services afterwards
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var rooms1 = svc1.Subscribe(category1))
            using (var rooms2 = svc1.Subscribe(category2))
            {
                Assert.Empty(rooms1.Rooms);

                // These are disposed manually, but keep in a using block so that they are disposed even
                // if the test throws.
                using (var svc2 = matchmakingServiceFactory_(2))
                using (var svc3 = matchmakingServiceFactory_(3))
                {
                    // Create rooms from svc2 and svc3
                    var room2_1 = svc2.PublishAsync(category1, "conn1", null, cts.Token).Result;
                    var room2_2 = svc2.PublishAsync(category2, "conn2", null, cts.Token).Result;
                    var room3_1 = svc3.PublishAsync(category1, "conn3", null, cts.Token).Result;

                    // They should show up in svc1
                    {
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(rooms1, rl => rl.Count() == 2, cts.Token);
                        Assert.Equal(2, res1.Count());
                        Assert.Contains(res1, room => room.UniqueId == room2_1.UniqueId);
                        Assert.Contains(res1, room => room.UniqueId == room3_1.UniqueId);

                        var res2 = Utils.QueryAndWaitForRoomsPredicate(rooms2, rl => rl.Count() == 1, cts.Token);
                        Assert.Equal(room2_2.UniqueId, res2.First().UniqueId);
                    }

                    // After svc2 is shut down, its rooms should be gone from svc1
                    svc2.Dispose();
                    {
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(rooms1, rl => rl.Count() == 1, cts.Token);
                        Assert.Equal(room3_1.UniqueId, res1.First().UniqueId);
                        var res2 = Utils.QueryAndWaitForRoomsPredicate(rooms2, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res2);
                    }
                    // After svc3 is shut down, all rooms should be gone from svc1
                    svc3.Dispose();
                    {
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(rooms1, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res1);
                        var res2 = Utils.QueryAndWaitForRoomsPredicate(rooms2, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res2);
                    }
                }
            }
        }

        [Fact]
        public void CanEditRoomAttributes()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            {
                const string category = "CanEditRoomAttributes";
                var rooms1 = svc1.Subscribe(category);
                Assert.Empty(rooms1.Rooms);

                using (var svc2 = matchmakingServiceFactory_(2))
                {
                    // Create rooms from svc2
                    var origAttrs = new Dictionary<string, string> { { "keyA", "valA" }, { "keyB", "valB" } };
                    var room2 = svc2.PublishAsync(category, "conn1", origAttrs, cts.Token).Result;

                    // It should show up in svc1
                    {
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        var room1 = res1.First();
                        Assert.Equal(room2.UniqueId, room1.UniqueId);
                        Utils.AssertSameDictionary(res1.First().Attributes, origAttrs);
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
                        Utils.AssertSameDictionary(room2.Attributes, newAttrs);

                        // And remotely. This currently waits for the attributes to change. It would be nice if we could inspect the network instead.
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any() && rl.First().Attributes.Count == 1, cts.Token);
                        Assert.Single(res1);
                        var room1 = res1.First();
                        Assert.Equal(room2.UniqueId, room1.UniqueId);
                        Utils.AssertSameDictionary(res1.First().Attributes, newAttrs);
                    }
                }
            }
        }
    }

    public class PeerDiscoveryTestUdp : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeMatchmakingService(int userIndex)
        {
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0xffffff7f), 45277, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { RoomExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestUdp() : base(MakeMatchmakingService) { }
    }

    public class PeerDiscoveryTestUdpMulticast : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeMatchmakingService(int userIndex)
        {
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0x000000e0), 45278, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { RoomExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestUdpMulticast() : base(MakeMatchmakingService) { }
    }

    public class PeerDiscoveryTestMemory : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeMatchmakingService(int userIndex)
        {
            var net = new MemoryPeerDiscoveryTransport(userIndex);
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { RoomExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestMemory() : base(MakeMatchmakingService) { }
    }

    // Uses relay socket to reorder packets delivery.
    public class PeerDiscoveryTestReordered : PeerDiscoveryTest, IDisposable
    {
        private const int MaxDelayMs = 25;
        private const ushort Port = 45279;

        private readonly Socket relay_;
        private readonly Random random_;
        private readonly ITestOutputHelper output_;
        private readonly List<IPEndPoint> recipients_ = new List<IPEndPoint>();

        private IDiscoveryAgent MakeMatchmakingService(int userIndex)
        {
            // Peers all send packets to the relay.
            var address = new IPAddress(0x0000007f + (userIndex << 24));
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0xfeffff7f), Port, address);
            lock(recipients_)
            {
                recipients_.Add(new IPEndPoint(address, Port));
            }
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { RoomExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestReordered(ITestOutputHelper output) : base(null)
        {
            matchmakingServiceFactory_ = MakeMatchmakingService;
            output_ = output;

            relay_ = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            relay_.Bind(new IPEndPoint(new IPAddress(0xfeffff7f), Port));

            var seed = new Random().Next();
            output_.WriteLine($"Seed for lossy network: {seed}");
            random_ = new Random(seed);

            // Disable exception on UDP connection reset (don't care).
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            relay_.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        byte[] buf_ = new byte[1024];
                        var result = await relay_.ReceiveAsync(new ArraySegment<byte>(buf_), SocketFlags.None);

                        // The relay sends the packets to all peers with a random delay.
                        IPEndPoint[] curRecipients;
                        lock(recipients_)
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
                                    relay_.SendToAsync(new ArraySegment<byte>(buf_, 0, result), SocketFlags.None, rec);
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

        [Fact]
        public void AttributeEditsAreInOrder()
        {
            const string category = "AttributeEditsAreInOrder";
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var discovery = svc1.Subscribe(category))
            using (var svc2 = matchmakingServiceFactory_(2))
            {
                // Create room from svc2
                var origAttrs = new Dictionary<string, string> { { "value", "0" } };
                var room2 = svc2.PublishAsync(category, "conn1", origAttrs, cts.Token).Result;

                int lastValueSeen1 = 0;
                int lastValueSeen2 = 0;
                int lastValueCommitted = 0;
                for (int i = 0; i < 10; ++i)
                {
                    // Commit a bunch of edits in service2
                    for (int j = 0; j < 10; ++j)
                    {
                        {
                            ++lastValueCommitted;
                            var edit2 = room2.RequestEdit();
                            Assert.NotNull(edit2);
                            edit2.PutAttribute("value", lastValueCommitted.ToString());
                            var task = edit2.CommitAsync();
                            task.Wait(cts.Token);
                        }
                    }

                    // Give some time to the messages to reach the peers.
                    Task.Delay(MaxDelayMs).Wait();

                    // Edits should show up in svc1 in order
                    {
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(discovery, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        var room1 = res1.First();
                        Assert.Equal(room2.UniqueId, room1.UniqueId);
                        var value = int.Parse(room1.Attributes["value"]);
                        Assert.True(lastValueSeen1 <= value);
                        lastValueSeen1 = value;
                    }

                    // Edits should show up in svc2 in order
                    {
                        var value = int.Parse(room2.Attributes["value"]);
                        Assert.True(lastValueSeen2 <= value);
                        lastValueSeen2 = value;
                    }
                }
                {
                    // Eventually the last edit should be delivered to both services.
                    Func<IEnumerable<IDiscoveryResource>, bool> lastEditWasApplied = rl =>
                    {
                        return rl.Any() && rl.First().Attributes.TryGetValue("value", out string value) &&
                            int.Parse(value) == lastValueCommitted;
                    };
                    var res1 = Utils.QueryAndWaitForRoomsPredicate(svc1, category, lastEditWasApplied, cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(int.Parse(room2.Attributes["value"]), lastValueCommitted);
                }
            }
        }

        [Fact]
        public void NoAnnouncementsAfterDispose()
        {
            const string category = "NoAnnouncementsAfterDispose";
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = matchmakingServiceFactory_(1))
            using (var discovery = svc1.Subscribe(category))
            {
                using (var svc2 = matchmakingServiceFactory_(2))
                {
                    // Create a lot of rooms.
                    var tasks = new Task<IDiscoveryResource>[100];
                    for (int i = 0; i < tasks.Length; ++i)
                    {
                        tasks[i] = svc2.PublishAsync(category, "conn1", null, cts.Token);
                    }

                    // Wait until svc1 discovers at least one room.
                    var res1 = Utils.QueryAndWaitForRoomsPredicate(discovery, rl => rl.Any(), cts.Token);
                    Assert.NotNull(res1);
                    // Dispose the service.
                }

                // Eventually there should be no rooms left.
                {
                    var res1 = Utils.QueryAndWaitForRoomsPredicate(discovery, rl => !rl.Any(), cts.Token);
                    Assert.NotNull(res1);
                }
            }
        }
    }
}