using Autofac;
using Microsoft.MixedReality.Sharing.Channels;
using Microsoft.MixedReality.Sharing.Network.Test.Mocks;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Threading;
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
                .As<IChannelFactory<ReliableChannel>>()
                .As<IChannelFactory<UnreliableChannel>>()
                .As<IChannelFactory<ReliableOrderedChannel>>()
                .As<IChannelFactory<UnreliableOrderedChannel>>()
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

        [TestMethod]
        public async Task SendMessage()
        {
            string messageText = "This is a simple test.";

            using (ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope())
            using (ISession host = scope.Resolve<ISession>())
            using (ISession client = ((MockSession)host).CreateConection())
            using (ReliableChannel hostChannel = await host.GetChannelAsync<ReliableChannel>(CancellationToken.None))
            using (ReliableChannel clientChannel = await client.GetChannelAsync<ReliableChannel>(CancellationToken.None))
            {
                // Listen to messages
                TaskCompletionSource<byte[]> messageReceived = new TaskCompletionSource<byte[]>();
                void MessageReceived(IEndpoint endpoint, byte[] obj)
                {
                    messageReceived.SetResult(obj);
                }
                hostChannel.MessageReceived += MessageReceived;

                try
                {
                    await clientChannel.SendMessageAsync(Encoding.ASCII.GetBytes(messageText), CancellationToken.None);


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

        [TestMethod]
        public async Task BroadcastAudioMessage()
        {
            void AdditionalDependencies(ContainerBuilder containerBuilder)
            {
                containerBuilder.RegisterType<DefaultAudioChannelFactory<ReliableChannel>>()
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
            using (ISession client = ((MockSession)host).CreateConection())
            using (AudioChannel hostChannel = await host.GetChannelAsync<AudioChannel>(CancellationToken.None))
            using (AudioChannel clientChannel = await client.GetChannelAsync<AudioChannel>(CancellationToken.None))
            {
                // CLIENT Start streaming audio
                using (Stream stream = new MemoryStream())
                {
                    clientChannel.BeginStreamingAudio(stream);

                    try
                    {

                    }
                    finally
                    {
                        clientChannel.StopStreamingAudio();
                    }
                }
            }
        }
    }
}
