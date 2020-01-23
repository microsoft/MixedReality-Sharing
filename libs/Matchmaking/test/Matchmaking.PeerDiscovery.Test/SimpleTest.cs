// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Test
{
    public class SimpleTest
    {
        private void SendAndReceive(IDiscoveryAgent agent)
        {
            // Do simple operations to verify that sending and receiving packets doesn't fail.
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            {
                var _ = agent.PublishAsync("Test", "http://resource1", null, cts.Token).Result;
            }
            using (var task = agent.Subscribe("Category")) { }
        }

        [Fact]
        public void Broadcast()
        {
            using (var svc1 = new PeerDiscoveryAgent(new UdpPeerDiscoveryTransport(new IPAddress(0xffffffff), 45279)))
            {
                SendAndReceive(svc1);
            }
        }
        [Fact]
        public void Multicast()
        {
            using (var svc1 = new PeerDiscoveryAgent(new UdpPeerDiscoveryTransport(new IPAddress(0x000000e0), 45280)))
            {
                SendAndReceive(svc1);
            }
        }

        [Fact]
        public void LargePacketWarning()
        {
            var mem = new System.IO.MemoryStream();
            var listener = new TextWriterTraceListener(mem);
            Trace.Listeners.Add(listener);

            var net = new UdpPeerDiscoveryTransport(new IPAddress(0x000000e0), 45280);
            net.Start();
            net.Broadcast(Guid.Empty, new System.ArraySegment<byte>(new byte[2048]));
            net.Stop();

            Trace.Flush();
            Trace.Listeners.Remove(listener);
            var msg = System.Text.Encoding.UTF8.GetString(mem.ToArray());
            Assert.Contains("Large UDP", msg);
        }

        [Fact]
        public void ResourceExpiresOnTime()
        {
            var random = new Random();
            const int timeoutSec = 1;
            var network1 = new MemoryPeerDiscoveryTransport(random.Next());
            var network2 = new MemoryPeerDiscoveryTransport(random.Next());
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = new PeerDiscoveryAgent(network1))
            {
                const string category = "ResourceExpiresOnTime";
                var resources1 = svc1.Subscribe(category);
                Assert.Empty(resources1.Resources);

                using (var svc2 = new PeerDiscoveryAgent(network2, new PeerDiscoveryAgent.Options { ResourceExpirySec = timeoutSec }))
                {
                    // Create resources from svc2
                    var resource1 = svc2.PublishAsync(category, "conn1", null, cts.Token).Result;

                    // It should show up in svc1 even after the timeout.
                    {
                        Task.Delay(timeoutSec * 1200).Wait();
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        Assert.Equal(resource1.UniqueId, res1.First().UniqueId);
                    }

                    // Stop the network.
                    network1.Stop();

                    // Wait a bit after the timeout.
                    Task.Delay(timeoutSec * 1200).Wait();
                    {
                        var res1 = Utils.QueryAndWaitForResourcesPredicate(svc1, category, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res1);
                    }
                }
            }
        }
    }
}
