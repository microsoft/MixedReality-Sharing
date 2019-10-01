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

namespace Matchmaking.Local.Test
{
    public class SimpleTest
    {
        private void SendAndReceive(IMatchmakingService service)
        {
            // Do simple operations to verify that sending and receiving packets doesn't fail.
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            {
                var _ = service.CreateRoomAsync("CreateRoom", "http://room1", null, cts.Token).Result;
            }
            using (var task = service.StartDiscovery("Category")) { }
        }

        [Fact]
        public void Broadcast()
        {
            using (var svc1 = new PeerMatchmakingService(new UdpPeerNetwork(new IPAddress(0xffffffff), 45279)))
            {
                SendAndReceive(svc1);
            }
        }
        [Fact]
        public void Multicast()
        {
            using (var svc1 = new PeerMatchmakingService(new UdpPeerNetwork(new IPAddress(0x000000e0), 45280)))
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

            var net = new UdpPeerNetwork(new IPAddress(0x000000e0), 45280);
            net.Start();
            net.Broadcast(Guid.Empty, new System.ArraySegment<byte>(new byte[2048]));
            net.Stop();

            Trace.Flush();
            Trace.Listeners.Remove(listener);
            var msg = System.Text.Encoding.UTF8.GetString(mem.ToArray());
            Assert.Contains("Large UDP", msg);
        }

        [Fact]
        public void RoomExpiresOnTime()
        {
            const int timeoutSec = 1;
            var network1 = new MemoryPeerNetwork(1);
            var network2 = new MemoryPeerNetwork(2);
            using (var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            using (var svc1 = new PeerMatchmakingService(network1))
            {
                const string category = "RoomExpiresOnTime";
                var rooms1 = svc1.StartDiscovery(category);
                Assert.Empty(rooms1.Rooms);

                using (var svc2 = new PeerMatchmakingService(network2, new PeerMatchmakingService.Options { RoomExpirySec = timeoutSec }))
                {
                    // Create rooms from svc2
                    var room1 = svc2.CreateRoomAsync(category, "conn1", null, cts.Token).Result;

                    // It should show up in svc1 even after the timeout.
                    {
                        Task.Delay(timeoutSec * 1200).Wait();
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Any(), cts.Token);
                        Assert.Single(res1);
                        Assert.Equal(room1.UniqueId, res1.First().UniqueId);
                    }

                    // Stop the network.
                    network1.Stop();

                    // Wait a bit after the timeout.
                    Task.Delay(timeoutSec * 1200).Wait();
                    {
                        var res1 = Utils.QueryAndWaitForRoomsPredicate(svc1, category, rl => rl.Count() == 0, cts.Token);
                        Assert.Empty(res1);
                    }
                }
            }
        }
    }
}
