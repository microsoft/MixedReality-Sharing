// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Network;
using Microsoft.MixedReality.Sharing.Network.Socketer;
using System;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Xunit;

namespace Network.Socketer.Test
{
    class NetworkCtx : IDisposable
    {
        public IChannelCategoryFactory CFactory { get; protected set; }
        public IEndpointFactory EFactory { get; protected set; }

        public IChannelCategory UnorderedCategory { get; private set; }
        public IChannelCategory OrderedCategory { get; private set; }

        protected void InitCategories()
        {
            UnorderedCategory = CFactory.Create("Unordered", ChannelType.Unordered);
            OrderedCategory = CFactory.Create("Ordered", ChannelType.Ordered);
        }

        public void Dispose()
        {
            UnorderedCategory.Dispose();
            OrderedCategory.Dispose();
            CFactory.Dispose();
        }
    }

    class SocketerNetworkCtx : NetworkCtx
    {
        public SocketerNetworkCtx(int port)
        {
            CFactory = new SocketerChannelCategoryFactory(port);
            EFactory = new SocketerEndpointFactory();
            InitCategories();
        }
    }

    public class UnitTest
    {
        private void SetupContexts(out NetworkCtx a, out NetworkCtx b)
        {
            a = new SocketerNetworkCtx(45678);
            b = new SocketerNetworkCtx(45679);
        }

        [Fact]
        public void TestCategories()
        {
            NetworkCtx ctxA, ctxB;
            SetupContexts(out ctxA, out ctxB);

            Assert.ThrowsAny<Exception>(() => new SocketerChannelCategoryFactory(45678));

            ctxA.Dispose();
            ctxB.Dispose();
        }

        [Fact]
        public void TestUnorderedSendReceive()
        {
            NetworkCtx ctxA, ctxB;
            SetupContexts(out ctxA, out ctxB);

            var categoryA = ctxA.UnorderedCategory;
            var categoryB = ctxB.UnorderedCategory;

            var queueA = new MessageQueue();
            categoryA.Queue = queueA;

            string endpointAId = "localhost;45678";
            string endpointBId = "localhost;45679";

            IEndpoint endpointA = ctxA.EFactory.GetEndpoint(endpointAId);
            IEndpoint endpointB = ctxB.EFactory.GetEndpoint(endpointBId);

            IChannel aTob = endpointB.CreateChannel(categoryA);

            byte[] testMessage = Encoding.UTF8.GetBytes("testPayload");

            aTob.SendMessage(testMessage);

            var queueB = new MessageQueue();
            categoryB.Queue = queueB;

            aTob.SendMessage(testMessage);

            {
                IMessage received = queueB.Dequeue();
                Assert.Same(categoryB, received.Category);
                Assert.Equal(testMessage, received.Payload);
            }


            using (IChannel bToA = endpointA.CreateChannel(categoryB))
            {
                bToA.SendMessage(testMessage);
                IMessage received = queueA.Dequeue();
                Assert.Same(categoryA, received.Category);
                Assert.Equal(testMessage, received.Payload);
            }

            aTob.Dispose();

            ctxA.Dispose();
            ctxB.Dispose();
        }

        [Fact]
        public void TestOrderedSendReceive()
        {
            NetworkCtx ctxA, ctxB;
            SetupContexts(out ctxA, out ctxB);

            var categoryA = ctxA.OrderedCategory;
            var categoryB = ctxB.OrderedCategory;

            var queueA = new MessageQueue();
            categoryA.Queue = queueA;

            string endpointAId = "localhost;45678";
            string endpointBId = "localhost;45679";

            IEndpoint endpointA = ctxA.EFactory.GetEndpoint(endpointAId);
            IEndpoint endpointB = ctxB.EFactory.GetEndpoint(endpointBId);

            IChannel aTob = endpointB.CreateChannel(categoryA);

            byte[] testMessage = Encoding.UTF8.GetBytes("testPayload");

            aTob.SendMessage(testMessage);

            var queueB = new MessageQueue();
            categoryB.Queue = queueB;

            aTob.SendMessage(testMessage);

            IEndpoint sendEndpointA;
            {
                IMessage received = queueB.Dequeue();
                Assert.Same(categoryB, received.Category);
                Assert.Equal(testMessage, received.Payload);
                sendEndpointA = received.Sender;
            }

            using (IChannel bToA = endpointA.CreateChannel(categoryB))
            {
                bToA.SendMessage(testMessage);
                IMessage received = queueA.Dequeue();
                Assert.Same(categoryA, received.Category);
                Assert.Equal(testMessage, received.Payload);
            }

            using (IChannel bToAReply = sendEndpointA.CreateChannel(categoryB))
            {
                bToAReply.SendMessage(testMessage);
                IMessage received = queueA.Dequeue();
                Assert.Same(categoryA, received.Category);
                Assert.Equal(testMessage, received.Payload);
            }

            aTob.Dispose();

            ctxA.Dispose();
            ctxB.Dispose();
        }
    }
}
