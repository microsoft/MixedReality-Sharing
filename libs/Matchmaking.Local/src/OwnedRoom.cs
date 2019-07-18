// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Local
{
    /// <summary>
    /// Room belonging to this host.
    /// </summary>
    class OwnedRoom : RoomBase, IDisposable
    {
        public readonly SocketerClient Server;
        public RoomVisibility Visibility;

        private List<int> clientIds_ = new List<int>();

        public OwnedRoom(MatchmakingService service,
            SocketerClient server,
            Dictionary<string, object> attributes,
            RoomVisibility visibility,
            MatchParticipant owner)
            : base(service, Guid.NewGuid(), owner.Host, (ushort)server.Port, attributes, DateTime.UtcNow /* TODO */, owner)
        {
            Server = server;
            Visibility = visibility;

            Server.Connected += (SocketerClient s, int id, string clientHost, int clientPort) =>
            {
                clientIds_.Add(id);
            };
            Server.Disconnected += (SocketerClient s, int id, string clientHost, int clientPort) =>
            {
                clientIds_.Remove(id);
            };
            Server.Message += (SocketerClient s, SocketerClient.MessageEvent ev) =>
            {
                // TODO
            };
            Server.Start();
        }

        public void Dispose()
        {
            Server.Stop();
        }

        public override Task LeaveAsync()
        {
            Server.Stop();
            return Task.CompletedTask;
        }

        public override Task SetAttributesAsync(Dictionary<string, object> attributes)
        {
            return Task.Run(() =>
            {
                if (attributes.Any())
                {
                    lock (attributes_)
                    {
                        foreach (var attr in attributes)
                        {
                            attributes_[attr.Key] = attr.Value;
                        }
                    }
                    // TODO publish the changes (check if values have changed?)
                }
            });
        }

        public override Task SetVisibility(RoomVisibility val)
        {
            if (val == Visibility)
            {
                return Task.CompletedTask;
            }
            return Task.Run(() =>
            {
                lock (this)
                {
                    if (val == Visibility)
                    {
                        return;
                    }
                    Visibility = val;
                    // TODO publish the changes
                }
            });
        }
    }
}
