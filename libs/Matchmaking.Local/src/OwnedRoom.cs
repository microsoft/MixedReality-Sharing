// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        public override event EventHandler<MessageReceivedArgs> MessageReceived;

        public OwnedRoom(MatchmakingService service,
            SocketerClient server,
            IEnumerable<KeyValuePair<string, object>> attributes,
            RoomVisibility visibility,
            MatchParticipant owner)
            : base(service, Guid.NewGuid(), server.Host, (ushort)server.Port, attributes, DateTime.UtcNow /* TODO */, owner)
        {
            Server = server;
            Visibility = visibility;

            Server.Disconnected += (SocketerClient s, int id, string clientHost, int clientPort) =>
            {
                // Remove the participant.
                RemoveParticipant(id);

                // Notify the other participants.
                var packet = Utils.CreateParticipantLeftPacket(id);
                foreach (var participant in Participants)
                {
                    Server.SendNetworkMessage(packet, participant.IdInRoom);
                }

            };
            Server.Message += (SocketerClient s, SocketerClient.MessageEvent ev) =>
            {
                // TODO offload message sending/callbacks to different thread
                if (Utils.IsAttrPacket(ev.Message))
                {
                    SetAttributesAsync(Utils.ParseAttrPacket(ev.Message));
                }
                else if (Utils.IsMessagePacket(ev.Message))
                {
                    int targetId = Utils.ParseMessageParticipant(ev.Message);
                    if (targetId == 0)
                    {
                        // Message to this participant. Raise the event locally.
                        var sender = Participants.First(p => p.IdInRoom == ev.SourceId);
                        MessageReceived?.Invoke(this, new MessageReceivedArgs(sender, Utils.ParseMessagePayload(ev.Message)));
                    }
                    else
                    {
                        // Forward to target participant.
                        byte[] retargeted = Utils.ChangeMessageParticipant(ev.Message, ev.SourceId);
                        server.SendNetworkMessage(retargeted, targetId);
                    }
                }
                else if (Utils.IsJoinRequestPacket(ev.Message))
                {
                    // Add the new participant.
                    var newParticipant = new RoomParticipant(ev.SourceId, Utils.ParseJoinRequestPacket(ev.Message));
                    var oldClients = Participants.Skip(1);
                    AddParticipant(newParticipant);

                    // Send list of participants as a series of PARJ packets.
                    foreach (var participant in Participants)
                    {
                        server.SendNetworkMessage(Utils.CreateParticipantJoinedPacket(participant), ev.SourceId);
                    }

                    // Announce the new participant to the other participants
                    var packet = Utils.CreateParticipantJoinedPacket(newParticipant);
                    foreach (var participant in oldClients)
                    {
                        Server.SendNetworkMessage(packet, participant.IdInRoom);
                    }

                    // Send the attributes to the new participant.
                    server.SendNetworkMessage(Utils.CreateAttrPacket(Attributes), ev.SourceId);
                }
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
            return SetAttributesAsync(attributes);
        }

        private Task SetAttributesAsync(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            return Task.Run(() =>
            {
                if (attributes.Any())
                {
                    byte[] packet = Utils.CreateAttrPacket(attributes);

                    // Lock in case multiple threads try to modify the attributes at the same time.
                    lock (this)
                    {
                        IReadOnlyDictionary<string, object> oldAttributes = Attributes;
                        Dictionary<string, object> newAttributes = new Dictionary<string, object>(oldAttributes.Count);
                        foreach (var attr in oldAttributes)
                        {
                            newAttributes.Add(attr.Key, attr.Value);
                        }
                        foreach (var attr in attributes)
                        {
                            newAttributes[attr.Key] = attr.Value;
                        }

                        // Forward the changes to the clients.
                        // TODO shouldn't send inside the lock but only enqueue packets
                        foreach (var participant in Participants.Skip(1))
                        {
                            Server.SendNetworkMessage(packet, participant.IdInRoom);
                        }

                        // Set the attributes locally.
                        Attributes = newAttributes;
                    }
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

        public override void SendMessage(RoomParticipant participant, byte[] message)
        {
            // TODO this does not properly handle disconnection
            if (!Participants.Any(p => p.IdInRoom == participant.IdInRoom))
            {
                throw new ArgumentException("Participant " + participant.IdInRoom + " is not in room " + Guid);
            }
            byte[] packet = Utils.CreateMessagePacket(0, message);
            Server.SendNetworkMessage(packet, participant.IdInRoom);
        }
    }
}
