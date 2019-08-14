using Autofac;
using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Sockets;
using Microsoft.MixedReality.Sharing.Sockets.Core;
using Microsoft.MixedReality.Sharing.Test.Mocks;
using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Test
{
    [TestClass]
    public class SimpleMatchmaking
    {
        private ILifetimeScope rootLifetimeScope;

        [TestInitialize]
        public void Initialize()
        {
            ContainerBuilder containerBuilder = new ContainerBuilder();

            containerBuilder.RegisterInstance(LoggingUtility.Logger).SingleInstance();

            containerBuilder.RegisterType<MockParticipantProvider>()
                .As<IParticipantProvider>(); // Not single instance to simulate different machines

            //containerBuilder.RegisterType<MockSessionFactory>()
            //    .As<ISessionFactory<UDPMulticastRoomConfiguration>>()
            //    .SingleInstance();

            containerBuilder.RegisterType<SocketSessionFactory>()
                .As<ISessionFactory<UDPMulticastRoomConfiguration>>()
                .SingleInstance();

            containerBuilder.RegisterInstance(new UDPMulticastSettings(IPAddress.Any, IPAddress.Parse("224.0.0.1"), 10000))
                .SingleInstance();

            containerBuilder.RegisterType<UDPMulticastMatchmakingService>()
                .As<IMatchmakingService>();

            rootLifetimeScope = containerBuilder.Build();
        }

        //[TestMethod]
        //public async Task CreateAndFindRoom()
        //{
        //    using (ILifetimeScope lifetimeScope = rootLifetimeScope.BeginLifetimeScope())
        //    using (IMatchmakingService hostService = lifetimeScope.Resolve<IMatchmakingService>())
        //    using (IMatchmakingService clientService = lifetimeScope.Resolve<IMatchmakingService>())
        //    using (IOwnedRoom hostRoom = await hostService.OpenRoomAsync(new Dictionary<string, string>() { { "Test", "Value" } }, CancellationToken.None))
        //    using (ISession clientRoom = await clientService.JoinSessionByIdAsync(hostRoom.Id, CancellationToken.None))
        //    {

        //        Assert.AreEqual(hostRoom.Id, clientRoom.Id);
        //        Assert.AreEqual(hostRoom.Owner, clientRoom.Owner);
        //        Assert.AreEqual(hostRoom.Attributes["Test"], clientRoom.Attributes["Test"]);
        //    }
        //}

        [TestMethod]
        public async Task CreateAndFindRoomIdentical()
        {
            Dictionary<string, string> attributes = new Dictionary<string, string>() { { "Test", "Value" } };

            using (ILifetimeScope lifetimeScope = rootLifetimeScope.BeginLifetimeScope())
            using (IMatchmakingService hostService = lifetimeScope.Resolve<IMatchmakingService>())
            using (IMatchmakingService clientService = lifetimeScope.Resolve<IMatchmakingService>())
            using (IOwnedRoom hostRoom = await hostService.OpenRoomAsync(attributes, CancellationToken.None))
            {
                //ISession clientRoomId = await clientService.JoinSessionByIdAsync(hostRoom.Id, CancellationToken.None);
                //ISession clientRoomRandom = await clientService.JoinRandomSessionAsync(new Dictionary<string, string>(), CancellationToken.None);
                //ISession clientRoomAttributesRandom = await clientService.JoinRandomSessionAsync(attributes, CancellationToken.None);

                IRoom clientRoomOwner = (await clientService.GetRoomsByOwnerAsync(hostRoom.Owner, CancellationToken.None)).FirstOrDefault();
                IRoom clientRoomAttributes = (await clientService.GetRoomsByAttributesAsync(attributes, CancellationToken.None)).FirstOrDefault();

                //Assert.AreSame(clientRoomId, clientRoomRandom);
                //Assert.AreSame(clientRoomId, clientRoomAttributesRandom);
                Assert.AreEqual(hostRoom.Id, clientRoomOwner.Id);
                Assert.AreSame(clientRoomOwner, clientRoomAttributes);
            }
        }

        [TestMethod]
        public async Task CreateAndJoinRoom()
        {
            using (ILifetimeScope lifetimeScope = rootLifetimeScope.BeginLifetimeScope())
            using (IMatchmakingService hostService = lifetimeScope.Resolve<IMatchmakingService>())
            using (IMatchmakingService clientService = lifetimeScope.Resolve<IMatchmakingService>())
            using (IOwnedRoom hostRoom = await hostService.OpenRoomAsync(new Dictionary<string, string>() { { "Test", "Value" } }, CancellationToken.None))
            using (ISession clientRoom = await clientService.JoinSessionByIdAsync(hostRoom.Id, CancellationToken.None))
            {
                Assert.AreEqual(1, hostRoom.Session.ConnectedEndpoints.Count);
            }
        }
    }
}
