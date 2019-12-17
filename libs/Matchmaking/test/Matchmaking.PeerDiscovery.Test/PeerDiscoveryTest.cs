// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Test
{
    public abstract class PeerDiscoveryTest
    {
        protected Func<int, IDiscoveryAgent> discoveryAgentFactory_;

        protected PeerDiscoveryTest(Func<int, IDiscoveryAgent> factory)
        {
            discoveryAgentFactory_ = factory;
        }

        [Fact]
        public void CreateResource()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = discoveryAgentFactory_(1))
            {
                var resource1 = svc1.PublishAsync("CreateResource", "http://resource1", null, cts.Token).Result;

                Assert.Equal("http://resource1", resource1.Connection);
                Assert.Empty(resource1.Attributes);

                var attributes = new Dictionary<string, string> { ["prop1"] = "1", ["prop2"] = "2" };
                var resource2 = svc1.PublishAsync("CreateResource", "foo://resource2", attributes, cts.Token).Result;

                Assert.Equal("foo://resource2", resource2.Connection);
                Assert.Equal("1", resource2.Attributes["prop1"]);
                Assert.Equal("2", resource2.Attributes["prop2"]);
            }
        }

        [Fact]
        public void FindResourcesLocalAndRemote()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = discoveryAgentFactory_(1))
            using (var svc2 = discoveryAgentFactory_(2))
            {
                // Create some resources in the first one
                const string category = "FindResourcesLocalAndRemote";
                var resource1 = svc1.PublishAsync(category, "Conn1", null, cts.Token).Result;
                var resource2 = svc1.PublishAsync(category, "Conn2", null, cts.Token).Result;
                var resource3 = svc1.PublishAsync(category, "Conn3", null, cts.Token).Result;

                // Discover them from the first service
                {
                    var resources = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Count() >= 3, cts.Token);
                    Assert.Equal(3, resources.Count());
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource1.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource2.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource3.UniqueId));
                }

                // And also from the second
                {
                    var resources = Utils.QueryAndWaitForResourcesPredicate(svc2, category, rl => rl.Count() >= 3, cts.Token);
                    Assert.Equal(3, resources.Count());
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource1.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource2.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource3.UniqueId));
                }
            }
        }

        [Fact]
        public void FindResourcesFromAnnouncement()
        {
            // start discovery, then start services afterwards

            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = discoveryAgentFactory_(1))
            using (var svc2 = discoveryAgentFactory_(2))
            {
                const string category = "FindResourcesFromAnnouncement";

                using (var task1 = svc1.Subscribe(category))
                using (var task2 = svc2.Subscribe(category))
                {
                    Assert.Empty(task1.Resources);
                    Assert.Empty(task2.Resources);

                    var resource1 = svc1.PublishAsync(category, "foo1", null, cts.Token).Result;

                    // local
                    var res1 = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Any(), cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(res1.First().UniqueId, resource1.UniqueId);
                    // remote
                    var res2 = Utils.QueryAndWaitForResourcesPredicate(svc2, category, rl => rl.Any(), cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(res1.First().UniqueId, resource1.UniqueId);
                }
            }
        }

        [Fact]
        public void AgentShutdownRemovesResources()
        {
            const string category1 = "AgentShutdownRemovesResources1";
            const string category2 = "AgentShutdownRemovesResources2";

            // start discovery, then start agents afterwards
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = discoveryAgentFactory_(1))
            using (var resources1 = svc1.Subscribe(category1))
            using (var resources2 = svc1.Subscribe(category2))
            {
                Assert.Empty(resources1.Resources);

                // These are disposed manually, but keep in a using block so that they are disposed even
                // if the test throws.
                using (var svc2 = discoveryAgentFactory_(2))
                using (var svc3 = discoveryAgentFactory_(3))
                {
                    // Create resources from svc2 and svc3
                    var resource2_1 = svc2.PublishAsync(category1, "conn1", null, cts.Token).Result;
                    var resource2_2 = svc2.PublishAsync(category2, "conn2", null, cts.Token).Result;
                    var resource3_1 = svc3.PublishAsync(category1, "conn3", null, cts.Token).Result;

                    // They should show up in svc1
                    {
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(resources1, rl => rl.Count() == 2, cts.Token);
                        Assert.Equal(2, res1.Count());
                        Assert.Contains(res1, resource => resource.UniqueId == resource2_1.UniqueId);
                        Assert.Contains(res1, resource => resource.UniqueId == resource3_1.UniqueId);

                        var res2 = Utils.QueryAndWaitForResourcesPredicate(resources2, rl => rl.Count() == 1, cts.Token);
                        Assert.Equal(resource2_2.UniqueId, res2.First().UniqueId);
                    }

                    // After svc2 is shut down, its resources should be gone from svc1
                    svc2.Dispose();
                    {
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(resources1, rl => rl.Count() == 1, cts.Token);
                        Assert.Equal(resource3_1.UniqueId, res1.First().UniqueId);
                        var res2 = Utils.QueryAndWaitForResourcesPredicate(resources2, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res2);
                    }
                    // After svc3 is shut down, all resources should be gone from svc1
                    svc3.Dispose();
                    {
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(resources1, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res1);
                        var res2 = Utils.QueryAndWaitForResourcesPredicate(resources2, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res2);
                    }
                }
            }
        }

        [Fact]
        public void CanEditResourceAttributes()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = discoveryAgentFactory_(1))
            {
                const string category = "CanEditResourceAttributes";
                var resources1 = svc1.Subscribe(category);
                Assert.Empty(resources1.Resources);

                using (var svc2 = discoveryAgentFactory_(2))
                {
                    // Create resources from svc2
                    var origAttrs = new Dictionary<string, string> { { "keyA", "valA" }, { "keyB", "valB" } };
                    var resource2 = svc2.PublishAsync(category, "conn1", origAttrs, cts.Token).Result;

                    // It should show up in svc1
                    {
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        var resource1 = res1.First();
                        Assert.Equal(resource2.UniqueId, resource1.UniqueId);
                        Utils.AssertSameDictionary(res1.First().Attributes, origAttrs);
                        Assert.Null(resource1.RequestEdit()); // remote edit not yet supported
                    }

                    // Commit edits in svc2
                    {
                        var edit2 = resource2.RequestEdit();
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
                        Utils.AssertSameDictionary(resource2.Attributes, newAttrs);

                        // And remotely. This currently waits for the attributes to change. It would be nice if we could inspect the network instead.
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Any() && rl.First().Attributes.Count == 1, cts.Token);
                        Assert.Single(res1);
                        var resource1 = res1.First();
                        Assert.Equal(resource2.UniqueId, resource1.UniqueId);
                        Utils.AssertSameDictionary(res1.First().Attributes, newAttrs);
                    }
                }
            }
        }
    }

    public class PeerDiscoveryTestUdp : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0xffffff7f), 45277, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestUdp() : base(MakeDiscoveryAgent) { }
    }

    public class PeerDiscoveryTestUdpMulticast : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0x000000e0), 45278, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestUdpMulticast() : base(MakeDiscoveryAgent) { }
    }

    public class PeerDiscoveryTestMemory : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            var net = new MemoryPeerDiscoveryTransport(userIndex);
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestMemory() : base(MakeDiscoveryAgent) { }
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

        private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            // Peers all send packets to the relay.
            var address = new IPAddress(0x0000007f + (userIndex << 24));
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0xfeffff7f), Port, address);
            lock(recipients_)
            {
                recipients_.Add(new IPEndPoint(address, Port));
            }
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestReordered(ITestOutputHelper output) : base(null)
        {
            discoveryAgentFactory_ = MakeDiscoveryAgent;
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
            using (var svc1 = discoveryAgentFactory_(1))
            using (var discovery = svc1.Subscribe(category))
            using (var svc2 = discoveryAgentFactory_(2))
            {
                // Create resource from svc2
                var origAttrs = new Dictionary<string, string> { { "value", "0" } };
                var resource2 = svc2.PublishAsync(category, "conn1", origAttrs, cts.Token).Result;

                int lastValueSeen1 = 0;
                int lastValueSeen2 = 0;
                int lastValueCommitted = 0;
                for (int i = 0; i < 10; ++i)
                {
                    // Commit a bunch of edits in svc2
                    for (int j = 0; j < 10; ++j)
                    {
                        {
                            ++lastValueCommitted;
                            var edit2 = resource2.RequestEdit();
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
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(discovery, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        var resource1 = res1.First();
                        Assert.Equal(resource2.UniqueId, resource1.UniqueId);
                        var value = int.Parse(resource1.Attributes["value"]);
                        Assert.True(lastValueSeen1 <= value);
                        lastValueSeen1 = value;
                    }

                    // Edits should show up in svc2 in order
                    {
                        var value = int.Parse(resource2.Attributes["value"]);
                        Assert.True(lastValueSeen2 <= value);
                        lastValueSeen2 = value;
                    }
                }
                {
                    // Eventually the last edit should be delivered to both agents.
                    Func<IEnumerable<IDiscoveryResource>, bool> lastEditWasApplied = rl =>
                    {
                        return rl.Any() && rl.First().Attributes.TryGetValue("value", out string value) &&
                            int.Parse(value) == lastValueCommitted;
                    };
                    var res1 = Utils.QueryAndWaitForResourcesPredicate(svc1, category, lastEditWasApplied, cts.Token);
                    Assert.Single(res1);
                    Assert.Equal(int.Parse(resource2.Attributes["value"]), lastValueCommitted);
                }
            }
        }

        [Fact]
        public void NoAnnouncementsAfterDispose()
        {
            const string category = "NoAnnouncementsAfterDispose";
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = discoveryAgentFactory_(1))
            using (var discovery = svc1.Subscribe(category))
            {
                using (var svc2 = discoveryAgentFactory_(2))
                {
                    // Create a lot of resources.
                    var tasks = new Task<IDiscoveryResource>[100];
                    for (int i = 0; i < tasks.Length; ++i)
                    {
                        tasks[i] = svc2.PublishAsync(category, "conn1", null, cts.Token);
                    }

                    // Wait until svc1 discovers at least one resource.
                    var res1 = Utils.QueryAndWaitForResourcesPredicate(discovery, rl => rl.Any(), cts.Token);
                    Assert.NotNull(res1);
                    // Dispose the agent.
                }

                // Eventually there should be no resources left.
                {
                    var res1 = Utils.QueryAndWaitForResourcesPredicate(discovery, rl => !rl.Any(), cts.Token);
                    Assert.NotNull(res1);
                }
            }
        }
    }
}