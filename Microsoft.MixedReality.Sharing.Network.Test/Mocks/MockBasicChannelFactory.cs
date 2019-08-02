﻿using Microsoft.MixedReality.Sharing.Channels;
using System;
using System.IO;
using System.Linq;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    internal class MockBasicChannelFactory : BasicDataChannelFactoryBase
    {
        public override string Name => "Mock Implementation of Basic Data Channel";

        protected override BasicDataChannel GetChannel(IEndpoint endpoint, string channelId)
        {
            MockEndpoint mockEndpoint = (MockEndpoint)endpoint;
            return new MockBasicDataChannel(channelId, mockEndpoint.Session, mockEndpoint);
        }

        protected override BasicDataChannel GetChannel(ISession session, string channelId)
        {
            MockSession mockSession = (MockSession)session;
            return new MockBasicDataChannel(channelId, mockSession, null);
        }

        private static void SendMessageImplementation(string channelId, Stream stream, MockSession ownerSession, MockEndpoint targetLocalEndpoint)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);

            if (targetLocalEndpoint != null)
            {
                if (!ownerSession.ConnectedEndpointsMap.TryGetValue(targetLocalEndpoint, out MockSession targetRemoteSession))
                {
                    throw new InvalidOperationException("Endpoint disconnected.");
                }

                //target remote endpoint represents this session in the other session endpoint list
                MockEndpoint targetRemoteEndpoint = targetRemoteSession.ConnectedEndpointsMap.FirstOrDefault(t => t.Value == ownerSession).Key;

                if (targetRemoteEndpoint == null)
                {
                    throw new InvalidOperationException("Something wrong with the mock connection.");
                }

                MockBasicDataChannel channel = (MockBasicDataChannel)targetRemoteEndpoint.GetChannel<BasicDataChannel>(channelId);
                channel.RaiseMessageReceived(targetRemoteEndpoint, bytes.AsSpan());
            }
            else
            {
                // Broadcast to all endpoints
                foreach (MockSession targetRemoteSession in ownerSession.ConnectedEndpointsMap.Values)
                {
                    //target remote endpoint represents this session in the other session endpoint list
                    MockEndpoint targetRemoteEndpoint = targetRemoteSession.ConnectedEndpointsMap.FirstOrDefault(t => t.Value == ownerSession).Key;

                    if (targetRemoteEndpoint == null)
                    {
                        throw new InvalidOperationException("Something wrong with the mock connection.");
                    }

                    MockBasicDataChannel channel = (MockBasicDataChannel)targetRemoteSession.GetChannel<BasicDataChannel>(channelId);
                    channel.RaiseMessageReceived(targetRemoteEndpoint, bytes.AsSpan());
                }
            }
        }

        private class MockBasicDataChannel : BasicDataChannel
        {
            private readonly MockSession ownerSession;
            private readonly MockEndpoint targetLocalEndpoint;

            public MockBasicDataChannel(string id, MockSession ownerSession, MockEndpoint targetLocalEndpoint)
                : base(id)
            {
                this.ownerSession = ownerSession;
                this.targetLocalEndpoint = targetLocalEndpoint;
            }

            internal new void RaiseMessageReceived(IEndpoint endpoint, ReadOnlySpan<byte> message)
            {
                base.RaiseMessageReceived(endpoint, message);
            }

            protected override void OnSendMessage(Stream stream)
            {
                SendMessageImplementation(Id, stream, ownerSession, targetLocalEndpoint);
            }
        }
    }
}
