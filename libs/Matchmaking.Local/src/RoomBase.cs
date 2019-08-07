// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.StateSync;
using Microsoft.MixedReality.Sharing.Utilities.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
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

        public IReadOnlyDictionary<string, object> Attributes
        {
            get => attributes_;
            protected set => attributes_ = value;
        }

        public volatile IReadOnlyDictionary<string, object> attributes_;

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
            IEnumerable<KeyValuePair<string, object>> attributes,
            DateTime lastHeard)
        {
            service_ = service;
            Guid = guid;
            Host = host;
            Port = port;
            LastHeard = lastHeard;
            var attrDict = new Dictionary<string, object>();
            if (attributes != null)
            {
                foreach (var attr in attributes)
                {
                    attrDict.Add(attr.Key, attr.Value);
                }
            }
            Attributes = attrDict;
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
    abstract class RoomBase : RoomInfo, IRoom, IDisposable
    {
        public IParticipant Owner { get; }

        public IEnumerable<IParticipant> Participants { get; }

        //public IStateSubscription State => throw new NotImplementedException();

        public event EventHandler AttributesChanged;

        public abstract event EventHandler<MessageReceivedArgs> MessageReceived;

        protected void RaiseAttributesChanged()
        {
            AttributesChanged?.Invoke(this, new EventArgs());
        }

        public RoomBase(
            MatchmakingService service,
            Guid guid,
            string host,
            ushort port,
            IEnumerable<KeyValuePair<string, object>> attributes,
            DateTime lastHeard,
            IParticipant owner)
            : base(service,
                  guid,
                  host,
                  port,
                  attributes,
                  lastHeard)
        {
            Owner = owner;
            //Participants = new RoomParticipant[] { Owner };
        }

        public RoomBase(RoomInfo rhs, IParticipant owner)
            : base(rhs)
        {
            Owner = owner;
            //Participants = new RoomParticipant[] { Owner };
        }

        public abstract Task LeaveAsync();

        public abstract Task SetAttributesAsync(Dictionary<string, object> attributes);

        // TODO figure out what to do with this, e.g. move to public API or change to something else.
        public abstract void SendMessage(RoomParticipant participant, byte[] message);

        protected void AddParticipant(RoomParticipant participant)
        {
            var oldParticipants = Participants;
            if (oldParticipants.Any(p => p.IdInRoom == participant.IdInRoom))
            {
                throw new ArgumentException("Room " + Guid +" already contains participant " + participant.IdInRoom);
            }
            var newParticipants = new RoomParticipant[oldParticipants.Length + 1];
            Array.Copy(oldParticipants, newParticipants, oldParticipants.Length);
            newParticipants[oldParticipants.Length] = participant;
            Participants = newParticipants;
        }
        protected void RemoveParticipant(int idInRoom)
        {
            var oldParticipants = Participants;
            var newParticipants = oldParticipants.Where(p => p.IdInRoom != idInRoom).ToArray();
            if (newParticipants.Length != oldParticipants.Length - 1)
            {
                throw new ArgumentException("Room " + Guid +" does not contain participant " + idInRoom);
            }
            Participants = newParticipants;
        }

        public abstract void Dispose();
    }

    class RoomParticipant : IRoomParticipant
    {
        public int IdInRoom { get; }
        public IMatchParticipant MatchParticipant { get; }

        public RoomParticipant(int id, MatchParticipant matchParticipant)
        {
            IdInRoom = id;
            MatchParticipant = matchParticipant;
        }
    }

    public class MessageReceivedArgs
    {
        public readonly IRoomParticipant Sender;
        public readonly byte[] Payload;

        internal MessageReceivedArgs(IRoomParticipant sender, byte[] payload)
        {
            Sender = sender;
            Payload = payload;
        }
    }
}
