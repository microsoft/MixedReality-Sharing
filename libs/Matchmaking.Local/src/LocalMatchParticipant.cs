// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Local
{

    public class LocalMatchParticipant : IMatchParticipant
    {
        public string Id { get; }

        public string DisplayName { get; }

        public bool IsOnline { get; internal set; } = true;

        public string Host { get; internal set; }
        public ushort Port { get; internal set; }

        internal LocalMatchParticipant(string id, string displayName, string host = null, ushort port = 0)
        {
            Id = id;
            DisplayName = displayName;
            Host = host;
            Port = port;
        }
    }

    public class LocalMatchParticipantFactory : IMatchParticipantFactory
    {
        private readonly List<LocalMatchParticipant> knownParticipants_ = new List<LocalMatchParticipant>();

        public string LocalParticipantId => knownParticipants_[0].Id;
        public LocalMatchParticipant LocalParticipant => knownParticipants_[0];

        public LocalMatchParticipantFactory(string localId, string localName, string localAddress, ushort port)
        {
            knownParticipants_.Add(new LocalMatchParticipant(localId, localName, localAddress, port));
        }

        public Task<IMatchParticipant> GetParticipantAsync(string id, CancellationToken cancellationToken)
        {
            return Task.Run<IMatchParticipant>(() => knownParticipants_.Find(p => p.Id.Equals(id)));
        }
    }
}
