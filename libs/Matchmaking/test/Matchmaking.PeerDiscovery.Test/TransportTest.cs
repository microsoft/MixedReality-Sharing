using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Test
{
    public class TransportTest
    {
        private readonly Random random_;

        public TransportTest(ITestOutputHelper output)
        {
            var seed = new Random().Next();
            output.WriteLine($"Seed for lossy network: {seed}");
            random_ = new Random(seed);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestMemory(bool useMultiThread) { SendReceive(new MemoryTransportBuilder(), useMultiThread); }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUdp(bool useMultiThread) { SendReceive(new UdpTransportBuilder(), useMultiThread); }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUdpMulticast(bool useMultiThread) { SendReceive(new UdpMulticastTransportBuilder(), useMultiThread); }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUdpReordered(bool useMultiThread)
        {
            SendReceive(new UdpReorderedTransportBuilder(random_, false), useMultiThread);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUdpReorderedUnreliable(bool useMultiThread)
        {
            SendReceive(new UdpReorderedTransportBuilder(random_, true), useMultiThread);
        }

        private void SendReceive(ITransportBuilder transportBuilder, bool useMultiThread)
        {
            using(var cts = new CancellationTokenSource(Utils.TestTimeoutMs))
            {
                const int lastId = 50;
                const int guidsPerTransport = 3;

                // Make a few transports in the same broadcast domain.
                var transports = new IPeerDiscoveryTransport[5];

                // Associate a few stream IDs to each transport.
                int streamsNum = guidsPerTransport * transports.Length;
                var streams = new Guid[streamsNum];

                // Keep track of the messages received by each transport.
                var lastIdFromGuid = new Dictionary<Guid, int>[transports.Length];
                var lastMessageReceived = new Dictionary<Guid, ManualResetEvent>[transports.Length];


                for (int transportIdx = 0; transportIdx < transports.Length; ++transportIdx)
                {
                    // Initialize transport and associated data.
                    lastMessageReceived[transportIdx] = new Dictionary<Guid, ManualResetEvent>();
                    lastIdFromGuid[transportIdx] = new Dictionary<Guid, int>();
                    for (int guidIdx = 0; guidIdx < guidsPerTransport; ++guidIdx)
                    {
                        var streamId = Guid.NewGuid();
                        streams[transportIdx * guidsPerTransport + guidIdx] = streamId;
                        lastMessageReceived[transportIdx][streamId] = new ManualResetEvent(false);
                    }
                    transports[transportIdx] = transportBuilder.MakeTransport(transportIdx + 1);

                    // Handle received messages.
                    // Note: must copy into local variable or the lambda will capture the wrong value.
                    int captureTransportIdx = transportIdx;
                    transports[transportIdx].Message +=
                        (IPeerDiscoveryTransport _, IPeerDiscoveryMessage message) =>
                        {
                            // Check that messages are received in send order.
                            lastIdFromGuid[captureTransportIdx].TryGetValue(message.StreamId, out int lastReceivedId);
                            int currentId = BitConverter.ToInt32(message.Contents);
                            Assert.True(currentId > lastReceivedId);

                            // Notify that the last message hasn't been dropped.
                            if (currentId == lastId)
                            {
                                lastMessageReceived[captureTransportIdx][message.StreamId].Set();
                            }
                        };
                    transports[transportIdx].Start();
                }

                try
                {
                    // Send a sequence of messages from every transport for every stream.
                    var sendTasks = new List<Task>();
                    for (int transportIdx = 0; transportIdx < transports.Length; ++transportIdx)
                    {
                        for (int guidIdx = 0; guidIdx < guidsPerTransport; ++guidIdx)
                        {
                            var guid = streams[transportIdx * guidsPerTransport + guidIdx];
                            var transport = transports[transportIdx];
                            Action sendMessages = () =>
                            {
                                for (int msgIdx = 1; msgIdx <= lastId; ++msgIdx)
                                {
                                    byte[] bytes = BitConverter.GetBytes(msgIdx);
                                    transport.Broadcast(guid, bytes);
                                }
                            };
                            if (useMultiThread)
                            {
                                sendTasks.Add(Task.Run(sendMessages));
                            }
                            else
                            {
                                sendMessages();
                            }
                        }
                    }

                    // Wait until all messages have been sent.
                    Task.WaitAll(sendTasks.ToArray(), cts.Token);

                    // The last message has been received for every stream by every transport.
                    var allHandles = new List<WaitHandle>(streamsNum);
                    foreach (var transportEvents in lastMessageReceived)
                    {
                        foreach(var ev in transportEvents.Values)
                        {
                            allHandles.Add(ev);
                        }
                    }
                    // Workaround to WaitHandle.WaitAll not taking a CancellationToken.
                    Task.Run(() => WaitHandle.WaitAll(allHandles.ToArray())).Wait(cts.Token);
                }
                finally
                {
                    Exception anyException = null;
                    foreach (var transport in transports)
                    {
                        try
                        {
                            transport.Stop();
                        }
                        catch (Exception e)
                        {
                            anyException = e;
                        }
                    }
                    if (anyException != null)
                    {
                        throw anyException;
                    }
                }
            }
        }
    }
}
