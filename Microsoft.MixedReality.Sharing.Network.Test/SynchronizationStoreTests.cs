using Autofac;
using Microsoft.MixedReality.Sharing.Channels;
using Microsoft.MixedReality.Sharing.Network.Test.Mocks;
using Microsoft.MixedReality.Sharing.StateSync;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Test
{
    [TestClass]
    public class SynchronizationStoreTests
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

        [TestMethod] // won't run
        public async Task ReadCheckCommit()
        {
            using (MockSession session = rootLifetimeScope.Resolve<MockSession>())
            using (SynchronizationKey key = session.SynchronizationStore.CreateKey("SomeKeyString"))
            using (SynchronizationKey anotherKey = session.SynchronizationStore.CreateKey("AnotherKeyString"))
            {
                Transaction t = null;
                session.SynchronizationStore.UsingSnapshot(s =>
                {
                    float val = s.Get<float>(key);
                    if (val < 10)
                    {
                        t = s.CreateTransaction();
                    }
                });

                if (t != null)
                {
                    t.Require(key);
                    t.Set(anotherKey, 10);
                    TransactionResult result = await t.CommitAsync(CancellationToken.None);
                }
            }
        }

        [TestMethod]
        public void HandleEvents()
        {
            using (MockSession session = rootLifetimeScope.Resolve<MockSession>())
            using (SynchronizationKey key = session.SynchronizationStore.CreateKey("SomeKeyString"))
            using (SynchronizationKey anotherKey = session.SynchronizationStore.CreateKey("AnotherKeyString"))
            {
                session.SynchronizationStore.KeysChanged += KeysChanged;
                session.SynchronizationStore.KeysChanged -= KeysChanged;
            }
        }

        private void KeysChanged(LightweightKeySet updatedKeys, LightweightSnapshot snapshot)
        {
            
        }
    }
}
