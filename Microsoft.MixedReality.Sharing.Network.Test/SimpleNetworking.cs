using Autofac;
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
            containerBuilder.RegisterType<MockSimpleChannelFactory>()
                .As<IChannelFactory<MockSession, IMessage>>()
                .As<IChannelFactory<MockSession, ReliableMessage>>()
                .As<IChannelFactory<MockSession, UnreliableMessage>>()
                .As<IChannelFactory<MockSession, ReliableOrderedMessage>>()
                .As<IChannelFactory<MockSession, UnreliableOrderedMessage>>()
                .SingleInstance();
            containerBuilder.RegisterType<MockSession>();
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
            using (ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope())
            using (MockSession host = scope.Resolve<MockSession>())
            {
                // HOST
                IChannel<MockSession, ReliableMessage> hostSessionChannel = await host.GetChannelAsync<ReliableMessage>(CancellationToken.None);

                // Listen to messages
                TaskCompletionSource<ReliableMessage> messageReceived = new TaskCompletionSource<ReliableMessage>();
                void MessageReceived(IEndpoint<MockSession> endpoint, ReliableMessage obj)
                {
                    messageReceived.SetResult(obj);
                }
                hostSessionChannel.MessageReceived += MessageReceived;

                string messageText = "This is a simple test.";

                // CLIENT
                using (MockSession client = host.CreateConection())
                {
                    IChannel<MockSession, ReliableMessage> clientSessionChannel = await client.GetChannelAsync<ReliableMessage>(CancellationToken.None);
                    await clientSessionChannel.SendMessageAsync(new ReliableMessage(Encoding.ASCII.GetBytes(messageText)), CancellationToken.None);
                }

                // HOST process
                ReliableMessage message = await messageReceived.Task;

                using (StreamReader reader = new StreamReader(await message.OpenReadAsync(CancellationToken.None)))
                {
                    Assert.AreEqual(messageText, await reader.ReadToEndAsync());
                }

                hostSessionChannel.MessageReceived -= MessageReceived;
            }
        }

        [TestMethod]
        public async Task BroadcastAudioMessage()
        {
            void AdditionalDependencies(ContainerBuilder containerBuilder)
            {
                //containerBuilder.RegisterType<AudioChannelFactory<MockSession>>()
                //    .WithParameter(new NamedParameter("reliableAndOrdered", true))
                //    .As<IChannelFactory<MockSession, IMessage>>()
                //    .SingleInstance();

                // Alternate
                containerBuilder.RegisterType<MockAudioChannelReplacementFactory>()
                    .As<IChannelFactory<MockSession, IMessage>>()
                    .SingleInstance();
            }

            using (ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope(AdditionalDependencies))
            using (MockSession session = scope.Resolve<MockSession>())
            using (IChannel<MockSession, AudioMessage> channel = await session.GetChannelAsync<AudioMessage>(CancellationToken.None))
            {
                string messageText = "Pretend this is an audio message.";

                // CLIENT Send message
                await channel.SendMessageAsync(new AudioMessage(Encoding.ASCII.GetBytes(messageText)), CancellationToken.None);
            }
        }
    }
}
