// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Test
{
    public abstract class PeerDiscoveryTest
    {
        protected Func<int, IDiscoveryAgent> discoveryAgentFactory_;
        private readonly ITestOutputHelper output_;
        protected readonly Random random_;

        protected PeerDiscoveryTest(Func<int, IDiscoveryAgent> factory, ITestOutputHelper output)
        {
            discoveryAgentFactory_ = factory;
            output_ = output;

            var seed = new Random().Next();
            output_.WriteLine($"Seed for lossy network: {seed}");
            random_ = new Random(seed);
        }

        protected abstract bool SimulatesPacketLoss { get; }

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
                var resource2 = svc2.PublishAsync(category, "Conn2", null, cts.Token).Result;

                // Discover them from the first service
                {
                    var resources = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Count() == 2, cts.Token);
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource1.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource2.UniqueId));
                }

                // And also from the second
                {
                    var resources = Utils.QueryAndWaitForResourcesPredicate(svc2, category, rl => rl.Count() == 2, cts.Token);
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource1.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource2.UniqueId));
                }

                // Add more resources
                var resource3 = svc1.PublishAsync(category, "Conn3", null, cts.Token).Result;
                var resource4 = svc2.PublishAsync(category, "Conn4", null, cts.Token).Result;

                // Discover them from the first service
                {
                    var resources = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Count() == 4, cts.Token);
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource1.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource2.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource3.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource4.UniqueId));
                }

                // And also from the second
                {
                    var resources = Utils.QueryAndWaitForResourcesPredicate(svc2, category, rl => rl.Count() == 4, cts.Token);
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource1.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource2.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource3.UniqueId));
                    Assert.Contains(resources, r => r.UniqueId.Equals(resource4.UniqueId));
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
            if (SimulatesPacketLoss)
            {
                // Shutdown message might not be retried and its loss will make this test fail. Skip it.
                return;
            }

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

        private void WaitWithCancellation(WaitHandle waitHandle, CancellationToken token)
        {
            WaitHandle.WaitAny(new WaitHandle[] { waitHandle, token.WaitHandle });
            token.ThrowIfCancellationRequested();
        }

        [Fact]
        public void CanDisposeSubscriptions()
        {
            const string category1 = "CanDisposeSubscriptions1";
            const string category2 = "CanDisposeSubscriptions2";
            IDiscoverySubscription surviving;
            IDiscoverySubscription survivingInHandler;

            var handlerEntered = new ManualResetEvent(false);
            var agentDisposed = new ManualResetEvent(false);

            var delayedException = new Utils.DelayedException();

            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var publishingAgent = discoveryAgentFactory_(1))
            {
                publishingAgent.PublishAsync(category1, "", null, cts.Token).Wait();
                publishingAgent.PublishAsync(category2, "", null, cts.Token).Wait();
                var agent = discoveryAgentFactory_(2);

                {
                    bool subIsDisposed = false;
                    var subDisposedEvent = new ManualResetEvent(false);
                    agent.Subscribe(category1).Updated +=
                        delayedException.Wrap<IDiscoverySubscription>(
                        sub =>
                        {
                            // Tests for https://github.com/microsoft/MixedReality-Sharing/issues/83.
                            if (sub.Resources.Any())
                            {
                                // Dispose the task from its handler.
                                sub.Dispose();
                                // The handler is not called after disposal.
                                Assert.False(subIsDisposed);
                                subIsDisposed = true;
                                subDisposedEvent.Set();
                            }
                        });
                    // Wait until the subscription is disposed before going on.
                    WaitWithCancellation(subDisposedEvent, cts.Token);
                }

                var survivingDisposedEvent = new ManualResetEvent(false);
                surviving = agent.Subscribe(category1);

                {
                    bool subIsDisposed = false;
                    survivingInHandler = agent.Subscribe(category2);
                    survivingInHandler.Updated +=
                        delayedException.Wrap<IDiscoverySubscription>(
                        sub =>
                        {
                            handlerEntered.Set();

                            // Wait until the agent is disposed by the main thread.
                            WaitWithCancellation(agentDisposed, cts.Token);

                            // Dispose the task from its handler.
                            sub.Dispose();
                            // The handler is not called after disposal.
                            Assert.False(subIsDisposed);
                            subIsDisposed = true;
                            survivingDisposedEvent.Set();
                        });
                }

                // Wait until the handler is entered.
                WaitWithCancellation(handlerEntered, cts.Token);

                // Dispose the agent while in the handler.
                agent.Dispose();

                // Signal the handler to go ahead and dispose the subscription.
                agentDisposed.Set();

                // The resources in the other surviving subscription are cleared eventually.
                while(surviving.Resources.Any())
                {
                    Task.Delay(1).Wait(cts.Token);
                }

                // Dispose the other surviving subscription.
                surviving.Dispose();

                WaitWithCancellation(survivingDisposedEvent, cts.Token);

                delayedException.Rethrow();
            }
        }

        [Fact]
        public void MultiThreadIsSafe()
        {
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            {
                var delayedException = new Utils.DelayedException();
                using (var svc1 = discoveryAgentFactory_(1))
                using (var svc2 = discoveryAgentFactory_(2))
                {
                    for (int i = 0; i < 1000; ++i)
                    {
                        {
                            var category = "DisposeInHandler" + i;
                            svc1.PublishAsync(category, "");
                            svc2.PublishAsync(category, "");
                            IDiscoverySubscription sub = svc2.Subscribe(category);
                            sub.Updated +=
                            delayedException.Wrap<IDiscoverySubscription>(
                                subscription =>
                                {
                                    Assert.True(subscription.Resources.Count() >= 0);
                                    subscription.Dispose();
                                });
                        }
                        {
                            var category = "DisposeInOtherThread" + i;
                            svc1.PublishAsync(category, "");
                            svc2.PublishAsync(category, "");
                            IDiscoverySubscription sub = svc2.Subscribe(category);
                            Task.Delay(random_.Next(0, 200)).ContinueWith(
                            delayedException.Wrap<Task>(
                                _ =>
                                {
                                    Assert.True(sub.Resources.Count() >= 0);
                                    sub.Dispose();
                                }));
                        }
                    }
                }
                Thread.Sleep(200);
                delayedException.Rethrow();
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

        public PeerDiscoveryTestUdp(ITestOutputHelper output) : base(MakeDiscoveryAgent, output) { }

        protected override bool SimulatesPacketLoss => false;
    }

    public class PeerDiscoveryTestUdpMulticast : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0x000000e0), 45278, new IPAddress(0x0000007f + (userIndex << 24)));
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestUdpMulticast(ITestOutputHelper output) : base(MakeDiscoveryAgent, output) { }
        protected override bool SimulatesPacketLoss => false;
    }

    public class PeerDiscoveryTestMemory : PeerDiscoveryTest
    {
        static private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            var net = new MemoryPeerDiscoveryTransport(userIndex);
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestMemory(ITestOutputHelper output) : base(MakeDiscoveryAgent, output) { }
        protected override bool SimulatesPacketLoss => false;
    }

    // Uses relay socket to reorder packets delivery.
    public abstract class PeerDiscoveryTestReordered : PeerDiscoveryTest, IDisposable
    {
        private const int MaxDelayMs = 25;
        private const int MaxRetries = 3;

        private readonly Socket relay_;
        private readonly ushort port_;
        private readonly List<IPEndPoint> recipients_ = new List<IPEndPoint>();

        // Wraps a packet for use in a map.
        private class Packet
        {
            public IPEndPoint EndPoint;
            public byte[] Contents;

            public Packet(IPEndPoint endPoint, ArraySegment<byte> contents)
            {
                EndPoint = endPoint;
                Contents = new byte[contents.Count];
                for (int i = 0; i < contents.Count; ++i)
                {
                    Contents[i] = contents[i];
                }
            }

            public override bool Equals(object other)
            {
                if (other is Packet rhs)
                {
                    return EndPoint.Equals(rhs.EndPoint) && Contents.SequenceEqual(rhs.Contents);
                }
                return false;
            }

            public override int GetHashCode()
            {
                // Not a great hash but it shouldn't matter in this case.
                var result = 0;
                foreach (byte b in Contents)
                {
                    result = (result * 31) ^ b;
                }
                return EndPoint.GetHashCode() ^ result;
            }
        }

        // Keeps track of each packet that goes through the relay and counts its repetitions.
        // See receive loop for usage.
        private readonly Dictionary<Packet, int> packetCounters;

        protected override bool SimulatesPacketLoss => (packetCounters != null);

        private IDiscoveryAgent MakeDiscoveryAgent(int userIndex)
        {
            // Peers all send packets to the relay.
            var address = new IPAddress(0x0000007f + (userIndex << 24));
            var net = new UdpPeerDiscoveryTransport(new IPAddress(0xfeffff7f), port_, address,
                new UdpPeerDiscoveryTransport.Options { MaxRetries = MaxRetries, MaxRetryDelayMs = 100 });
            lock (recipients_)
            {
                var endpoint = new IPEndPoint(address, port_);

                // Remove first so the same agent can be re-created multiple times
                recipients_.Remove(endpoint);
                recipients_.Add(endpoint);
            }
            return new PeerDiscoveryAgent(net, new PeerDiscoveryAgent.Options { ResourceExpirySec = int.MaxValue });
        }

        public PeerDiscoveryTestReordered(ITestOutputHelper output, bool packetLoss, ushort port) : base(null, output)
        {
            discoveryAgentFactory_ = MakeDiscoveryAgent;
            port_ = port;

            relay_ = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            relay_.Bind(new IPEndPoint(new IPAddress(0xfeffff7f), port_));

            // Disable exception on UDP connection reset (don't care).
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            relay_.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

            if (packetLoss)
            {
                packetCounters = new Dictionary<Packet, int>();
            }

            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        byte[] buf_ = new byte[1024];
                        var result = await relay_.ReceiveFromAsync(new ArraySegment<byte>(buf_), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));

                        if (packetCounters != null)
                        {
                            // Increase the probability of delivery of a packet with retries, up to 100% on the last retry.
                            // This simulates heavy packet loss while still guaranteeing that everything works.
                            // Note that this logic is very naive, but should be fine for small tests.
                            var packet = new Packet((IPEndPoint)result.RemoteEndPoint, new ArraySegment<byte>(buf_, 0, result.ReceivedBytes));
                            if (!packetCounters.TryGetValue(packet, out int counter))
                            {
                                counter = 0;
                            }

                            if (counter == MaxRetries - 1)
                            {
                                // Last retry, always send and forget the packet.
                                packetCounters.Remove(packet);
                            }
                            else
                            {
                                packetCounters[packet] = counter + 1;
                                // Drop with decreasing probability.
                                if (random_.Next(0, MaxRetries) > counter)
                                {
                                    continue;
                                }
                            }
                        }

                        // The relay sends the packets to all peers with a random delay.
                        IPEndPoint[] curRecipients;
                        lock (recipients_)
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
                                    relay_.SendToAsync(new ArraySegment<byte>(buf_, 0, result.ReceivedBytes), SocketFlags.None, rec);
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
    }

    public class PeerDiscoveryTestReorderedReliable : PeerDiscoveryTestReordered
    {
        public PeerDiscoveryTestReorderedReliable(ITestOutputHelper output) : base(output, packetLoss: false, 45279) { }

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
    public class PeerDiscoveryTestReorderedUnreliable : PeerDiscoveryTestReordered
    {
        public PeerDiscoveryTestReorderedUnreliable(ITestOutputHelper output) : base(output, packetLoss: true, 45280) { }
    }
}