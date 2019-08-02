using Autofac;
using Microsoft.MixedReality.Sharing.Channels;
using Microsoft.MixedReality.Sharing.Network.Test.Mocks;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Test
{
    [TestClass]
    public class MockNetworking
    {
        private ILifetimeScope rootLifetimeScope;

        [TestInitialize]
        public void Initialize()
        {
            ContainerBuilder containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterInstance(LoggingUtility.Logger).SingleInstance();

            containerBuilder.RegisterType<MockBasicChannelFactory>()
                .As<IChannelFactory<IChannel>>()
                .As<IChannelFactory<BasicDataChannel>>()
                .SingleInstance();

            containerBuilder.RegisterType<MockSession>().As<ISession>();

            rootLifetimeScope = containerBuilder.Build();
        }

        [TestCleanup]
        public void Cleanup()
        {
            rootLifetimeScope.Dispose();
            rootLifetimeScope = null;
        }

        /// <summary>
        /// Simple test to send receive a message.
        /// </summary>
        [TestMethod]
        public async Task SendMessage()
        {
            string messageText = "This is a simple test.";

            using (ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope())
            using (ISession host = scope.Resolve<ISession>())
            using (ISession client = ((MockSession)host).CreateConnection())
            using (BasicDataChannel hostChannel = host.GetChannel<BasicDataChannel>())
            using (BasicDataChannel clientChannel = client.GetChannel<BasicDataChannel>())
            {
                // Listen to messages
                TaskCompletionSource<byte[]> messageReceived = new TaskCompletionSource<byte[]>();
                void MessageReceived(IEndpoint endpoint, ReadOnlySpan<byte> obj)
                {
                    messageReceived.SetResult(obj.ToArray());
                }
                hostChannel.MessageReceived += MessageReceived;

                try
                {
                    clientChannel.SendMessage(Encoding.ASCII.GetBytes(messageText));


                    // HOST process
                    byte[] message = await messageReceived.Task;

                    Assert.AreEqual(messageText, Encoding.ASCII.GetString(message));
                }
                finally
                {
                    hostChannel.MessageReceived -= MessageReceived;
                }
            }
        }

        /// <summary>
        /// Simple method to test the streaming aspect of audio.
        /// </summary>
        [TestMethod]
        public async Task BroadcastAudioMessage()
        {
            void AdditionalDependencies(ContainerBuilder containerBuilder)
            {
                containerBuilder.RegisterType<DefaultAudioChannelFactory>()
                    .As<IChannelFactory<IChannel>>()
                    .As<IChannelFactory<AudioChannel>>()
                    .SingleInstance();

                // Alternate
                //containerBuilder.RegisterType<MockAudioChannelReplacementFactory>()
                //    .As<IChannelFactory<MockSession, IMessage>>()
                //    .SingleInstance();
            }

            using (ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope(AdditionalDependencies))
            using (ISession host = scope.Resolve<ISession>())
            using (ISession client = ((MockSession)host).CreateConnection())
            using (AudioChannel hostChannel = host.GetChannel<AudioChannel>())
            using (AudioChannel clientChannel = client.GetChannel<AudioChannel>())
            {
                byte[] randomBuffer = new byte[37907];
                new Random().NextBytes(randomBuffer);

                await Task.WhenAll(ReceiveDataAsync(hostChannel, randomBuffer), SendDataAsync(clientChannel, randomBuffer, 138, TimeSpan.FromMilliseconds(1)));
            }
        }

        private async Task ReceiveDataAsync(AudioChannel hostChannel, byte[] bufferToCompareWith)
        {
            await Task.Run(async () =>
            {
                Memory<byte> memory = new Memory<byte>(bufferToCompareWith);

                using (Stream stream = hostChannel.BeginListening())
                {
                    byte[] buffer = new byte[150];
                    Memory<byte> readingBuffer = new Memory<byte>(buffer);
                    for (int totalRead = 0; totalRead < bufferToCompareWith.Length && stream.CanRead;)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        int compareResult = memory.Slice(totalRead, bytesRead).Span.SequenceCompareTo(readingBuffer.Slice(0, bytesRead).Span);
                        Assert.AreEqual(compareResult, 0);

                        totalRead += bytesRead;
                    }
                }
            });
        }

        private async Task SendDataAsync(AudioChannel clientChannel, byte[] buffer, int sendPerLoopSize, TimeSpan loopSleepTime)
        {
            // CLIENT Start streaming audio
            using (Stream stream = clientChannel.BeginStreaming())
            {
                for (int i = 0; i < buffer.Length; i += sendPerLoopSize)
                {
                    await Task.Delay(loopSleepTime);
                    await stream.WriteAsync(buffer, i, Math.Min(buffer.Length - i, sendPerLoopSize));
                }
            }
        }
    }
}
