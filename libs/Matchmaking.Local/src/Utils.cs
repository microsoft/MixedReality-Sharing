// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Local
{
    static class Utils
    {
        private static readonly byte[] attrHeader_ = new byte[] { (byte)'A', (byte)'T', (byte)'T', (byte)'R' };
        private static readonly byte[] msgHeader_ = new byte[] { (byte)'M', (byte)'S', (byte)'S', (byte)'G' };
        private static readonly byte[] joinReqHeader_ = new byte[] { (byte)'J', (byte)'O', (byte)'I', (byte)'N' };
        private static readonly byte[] partJoinedHeader_ = new byte[] { (byte)'P', (byte)'A', (byte)'R', (byte)'J' };
        private static readonly byte[] partLeftHeader_ = new byte[] { (byte)'P', (byte)'A', (byte)'R', (byte)'L' };

        public static void WriteAttributes(IEnumerable<KeyValuePair<string, object>> attributes, MemoryStream str)
        {
            // Don't use .NET serialization for the whole map, wastes ~1KB per packet.
            var formatter = new BinaryFormatter();
            using (var writer = new BinaryWriter(str, Encoding.UTF8, true))
            {
                writer.Write(attributes.Count());
                foreach (var entry in attributes)
                {
                    writer.Write(entry.Key);
                }
            }
            foreach (var entry in attributes)
            {
                formatter.Serialize(str, entry.Value);
            }
        }

        public static KeyValuePair<string, object>[] ParseAttributes(MemoryStream str)
        {
            var formatter = new BinaryFormatter();
            int attrCount;
            string[] names;
            KeyValuePair<string, object>[] attributes;
            using (var reader = new BinaryReader(str, Encoding.UTF8, true))
            {
                attrCount = reader.ReadInt32();
                names = new string[attrCount];
                attributes = new KeyValuePair<string, object>[attrCount];
                for (int i = 0; i < attrCount; ++i)
                {
                    names[i] = reader.ReadString();
                }
            }
            for (int i = 0; i < attrCount; ++i)
            {
                // TODO this is insecure and not meant to be used in production code
                object value = formatter.Deserialize(str);
                attributes[i] = new KeyValuePair<string, object>(names[i], value);
            }
            return attributes;
        }

        public static bool IsAttrPacket(byte[] packet)
        {
            return packet.Take(attrHeader_.Length).SequenceEqual(attrHeader_);
        }

        public static byte[] CreateAttrPacket(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            var str = new MemoryStream();
            str.Write(attrHeader_, 0, attrHeader_.Length);
            WriteAttributes(attributes, str);
            return str.ToArray();
        }

        public static IEnumerable<KeyValuePair<string, object>> ParseAttrPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip ATTR header.
            str.Seek(attrHeader_.Length, SeekOrigin.Begin);
            return ParseAttributes(str);
        }
        public static bool IsMessagePacket(byte[] packet)
        {
            return packet.Take(msgHeader_.Length).SequenceEqual(msgHeader_);
        }

        public static byte[] CreateMessagePacket(int participantId, byte[] payload)
        {
            var res = new byte[msgHeader_.Length + 4 + payload.Length];
            // Header.
            Array.Copy(msgHeader_, res, msgHeader_.Length);
            // Participant (destination when sent from client to server, source when sent from server to client).
            Array.Copy(BitConverter.GetBytes(participantId), 0, res, msgHeader_.Length, 4);
            // Payload.
            Array.Copy(payload, 0, res, msgHeader_.Length + 4, payload.Length);
            return res;
        }

        public static int ParseMessageParticipant(byte[] packet)
        {
            return BitConverter.ToInt32(packet, msgHeader_.Length);
        }

        public static byte[] ParseMessagePayload(byte[] packet)
        {
            var res = new byte[packet.Length - msgHeader_.Length - 4];
            Array.Copy(packet, msgHeader_.Length + 4, res, 0, res.Length);
            return res;
        }

        public static byte[] ChangeMessageParticipant(byte[] packet, int newId)
        {
            var res = (byte[])packet.Clone();
            Array.Copy(BitConverter.GetBytes(newId), 0, res, msgHeader_.Length, 4);
            return res;
        }

        public static bool IsParticipantJoinedPacket(byte[] packet)
        {
            return packet.Take(partJoinedHeader_.Length).SequenceEqual(partJoinedHeader_);
        }

        public static byte[] CreateJoinRequestPacket(MatchParticipant participant)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(joinReqHeader_);
                writer.Write(participant.Id);
                writer.Write(participant.DisplayName);
            }
            return str.ToArray();
        }

        public static bool IsJoinRequestPacket(byte[] packet)
        {
            return packet.Take(joinReqHeader_.Length).SequenceEqual(joinReqHeader_);
        }

        public static MatchParticipant ParseJoinRequestPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            str.Seek(partJoinedHeader_.Length, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                string id = reader.ReadString();
                string displayName = reader.ReadString();
                return new MatchParticipant(id, displayName);
            }
        }

        public static byte[] CreateParticipantJoinedPacket(RoomParticipant participant)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(partJoinedHeader_);
                writer.Write(participant.IdInRoom);
                writer.Write(participant.MatchParticipant.Id);
                // TODO this should be stored/filled by MatchParticipantFactory
                writer.Write(participant.MatchParticipant.DisplayName);
            }
            return str.ToArray();
        }

        public static RoomParticipant ParseParticipantJoinedPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            str.Seek(partJoinedHeader_.Length, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                int idInRoom = reader.ReadInt32();
                string id = reader.ReadString();
                string displayName = reader.ReadString();
                return new RoomParticipant(idInRoom, new MatchParticipant(id, displayName));
            }
        }

        public static bool IsParticipantLeftPacket(byte[] packet)
        {
            return packet.Take(partLeftHeader_.Length).SequenceEqual(partLeftHeader_);
        }

        public static byte[] CreateParticipantLeftPacket(int participantId)
        {
            var res = new byte[partLeftHeader_.Length + 4];
            Array.Copy(partLeftHeader_, res, partLeftHeader_.Length);
            Array.Copy(BitConverter.GetBytes(participantId), 0, res, partLeftHeader_.Length, 4);
            return res;
        }

        public static int ParseParticipantLeftPacket(byte[] packet)
        {
            return BitConverter.ToInt32(packet, partLeftHeader_.Length);
        }
    }
}
