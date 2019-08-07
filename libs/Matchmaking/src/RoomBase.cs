// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public class RoomBase : IRoom
    {
        protected readonly object lockObject = new object();

        private IParticipant owner;
        private IReadOnlyCollection<IParticipant> participants;
        private IReadOnlyDictionary<string, string> attributes;

        public string Id { get; }

        public IParticipant Owner
        {
            get
            {
                lock (lockObject) { return owner; }
            }
        }

        public IReadOnlyCollection<IParticipant> Participants
        {
            get
            {
                lock (lockObject)
                {
                    return participants;
                }
            }
        }

        public IReadOnlyDictionary<string, string> Attributes
        {
            get
            {
                lock (lockObject) { return attributes; }
            }
        }

        protected RoomBase(string id)
        {
            Id = id;
        }

        public Task<ISession> JoinAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        protected void UpdateParticipants(IParticipant owner, ICollection<IParticipant> newParticipants)
        {
            lock (lockObject)
            {
                this.owner = owner;

                bool updated = participants == null || participants.Count != newParticipants.Count;
                if (!updated)
                {
                    foreach (IParticipant participant in participants)
                    {
                        if (!newParticipants.Contains(participant))
                        {
                            updated = true;
                            break;
                        }
                    }
                }

                if (updated)
                {
                    participants = new ReadOnlyCollectionWrapper<IParticipant>(newParticipants);
                }
            }
        }

        protected void UpdateAttributes(IDictionary<string, string> newAttributes)
        {
            lock (lockObject)
            {
                bool updated = attributes == null || newAttributes.Count != attributes.Count;
                if (!updated)
                {
                    foreach (KeyValuePair<string, string> newAttribute in newAttributes)
                    {
                        if (!attributes.TryGetValue(newAttribute.Key, out string existingValue) || !Equals(existingValue, newAttribute.Value))
                        {
                            updated = true;
                            break;
                        }
                    }
                }

                if (updated)
                {
                    attributes = new ReadOnlyDictionary<string, string>(newAttributes);
                }
            }
        }
    }
}
