// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing.Network.Socketer
{
    internal class SocketerEndpoint : IEndpoint
    {
        public string Host { get; }
        public int Port { get; }

        public string Id => GetId(Host, Port);

        public SocketerEndpoint(string host, int port)
        {
            Host = host;
            Port = port;
        }

        public IChannel CreateChannel(IChannelCategory category)
        {
            return ((SocketerChannelCategory)category).Create(this);
        }
        internal static string GetId(string host, int port)
        {
            return host + ";" + port;
        }
    }

    public class SocketerEndpointFactory : IEndpointFactory
    {
        public IEndpoint GetEndpoint(string id)
        {
            var pieces = id.Split(';');
            // TODO check for valid args
            return new SocketerEndpoint(pieces[0], int.Parse(pieces[1]));
        }
    }
}
