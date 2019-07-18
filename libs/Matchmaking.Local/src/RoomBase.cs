// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.StateSync;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Local
{
    /// <summary>
    /// Room on the local network.
    /// </summary>
    class RoomInfo : IRoomInfo
    {
        public string Id => Guid.ToString();
        public Guid Guid { get; }

        public IReadOnlyDictionary<string, object> Attributes => attributes_;

        internal Dictionary<string, object> attributes_ { get; } = new Dictionary<string, object>();

        // Connection info for the room.
        internal string Host { get; }
        internal ushort Port { get; }

        // TODO do something with this
        internal DateTime LastHeard;

        protected readonly MatchmakingService service_;

        public RoomInfo(
            MatchmakingService service,
            Guid guid,
            string host,
            ushort port,
            IReadOnlyDictionary<string, object> attributes,
            DateTime lastHeard)
        {
            service_ = service;
            Guid = guid;
            Host = host;
            Port = port;
            LastHeard = lastHeard;

            if (attributes != null)
            {
                foreach (var attr in attributes)
                {
                    attributes_.Add(attr.Key, attr.Value);
                }
            }
        }

        public RoomInfo(RoomInfo rhs)
            : this(rhs.service_,
                  rhs.Guid,
                  rhs.Host,
                  rhs.Port,
                  rhs.Attributes,
                  rhs.LastHeard)
        {
        }

        public Task<IRoom> JoinAsync(CancellationToken token = default)
        {
            return service_.JoinAsync(this, token);
        }
    }

    /// <summary>
    /// Base class for joined rooms.
    /// </summary>
    abstract class RoomBase : RoomInfo, IRoom
    {
        public IMatchParticipant Owner { get; }

        public IEnumerable<IMatchParticipant> Participants { get; } = new List<IMatchParticipant>();

        public IStateSubscription State => throw new NotImplementedException();

        public event EventHandler AttributesChanged;

        public RoomBase(
            MatchmakingService service,
            Guid guid,
            string host,
            ushort port,
            IReadOnlyDictionary<string, object> attributes,
            DateTime lastHeard,
            MatchParticipant owner)
            : base(service,
                  guid,
                  host,
                  port,
                  attributes,
                  lastHeard)
        {
            Owner = owner;
        }

        public RoomBase(RoomInfo rhs, MatchParticipant owner)
            : base(rhs)
        {
            Owner = owner;
        }


        public abstract Task LeaveAsync();

        public abstract Task SetAttributesAsync(Dictionary<string, object> attributes);

        public abstract Task SetVisibility(RoomVisibility val);
    }
}
