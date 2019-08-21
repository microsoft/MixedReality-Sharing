// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
#if false
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
        public const int AttrHeader = ('A' << 24) | ('T' << 16) | ('T' << 8) | 'R';
        public const int MsgHeader = ('M' << 24) | ('S' << 16) | ('S' << 8) | 'G';
        public const int JoinReqHeader = ('J' << 24) | ('O' << 16) | ('I' << 8) | 'N';
        public const int PartJoinedHeader = ('P' << 24) | ('A' << 16) | ('R' << 8) | 'J';
        public const int PartLeftHeader = ('P' << 24) | ('A' << 16) | ('R' << 8) | 'L';
        public const int RoomHeader = ('R' << 24) | ('O' << 16) | ('O' << 8) | 'M';

        public const int FindByIdHeader = ('F' << 24) | ('N' << 16) | ('D' << 8) | 'I';
        public const int FindByAttrHeader = ('F' << 24) | ('N' << 16) | ('D' << 8) | 'A';
        public const int FindByOwnerHeader = ('F' << 24) | ('N' << 16) | ('D' << 8) | 'O';
        public const int FindByParticipantsHeader = ('F' << 24) | ('N' << 16) | ('D' << 8) | 'P';

        private const int HeaderSize = sizeof(int);

        public static int ParseHeader(byte[] packet)
        {
            return BitConverter.ToInt32(packet, 0);
        }

        public static void WriteAttributes(IReadOnlyDictionary<string, object> attributes, MemoryStream str)
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

        public static IReadOnlyDictionary<string, object> ParseAttributes(MemoryStream str)
        {
            var formatter = new BinaryFormatter();
            int attrCount;
            string[] names;
            using (var reader = new BinaryReader(str, Encoding.UTF8, true))
            {
                attrCount = reader.ReadInt32();
                names = new string[attrCount];
                for (int i = 0; i < attrCount; ++i)
                {
                    names[i] = reader.ReadString();
                }
            }
            var attributes = new Dictionary<string, object>(attrCount);
            for (int i = 0; i < attrCount; ++i)
            {
                // TODO this is insecure and not meant to be used in production code
                object value = formatter.Deserialize(str);
                attributes.Add(names[i], value);
            }
            return attributes;
        }

        public static byte[] CreateAttrPacket(IReadOnlyDictionary<string, object> attributes)
        {
            var str = new MemoryStream();
            str.Write(BitConverter.GetBytes(AttrHeader), 0, HeaderSize);
            WriteAttributes(attributes, str);
            return str.ToArray();
        }

        public static IReadOnlyDictionary<string, object> ParseAttrPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip ATTR header.
            str.Seek(HeaderSize, SeekOrigin.Begin);
            return ParseAttributes(str);
        }

        public static byte[] CreateMessagePacket(int participantId, byte[] payload)
        {
            var str = new MemoryStream();
            using(var writer = new BinaryWriter(str))
            {
                // Header.
                writer.Write(MsgHeader);
                // Participant (destination when sent from client to server, source when sent from server to client).
                writer.Write(participantId);
                // Payload.
                writer.Write(payload);
            }
            return str.ToArray();
        }

        public static int ParseMessageParticipant(byte[] packet)
        {
            // Skip MSSG header.
            return BitConverter.ToInt32(packet, HeaderSize);
        }

        public static byte[] ParseMessagePayload(byte[] packet)
        {
            // Skip MSSG header and participant ID.
            var res = new byte[packet.Length - HeaderSize - sizeof(int)];
            Array.Copy(packet, HeaderSize + sizeof(int), res, 0, res.Length);
            return res;
        }

        public static byte[] ChangeMessageParticipant(byte[] packet, int newId)
        {
            var res = (byte[])packet.Clone();
            Array.Copy(BitConverter.GetBytes(newId), 0, res, HeaderSize, sizeof(int));
            return res;
        }

        public static byte[] CreateJoinRequestPacket(MatchParticipant participant)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(JoinReqHeader);
                writer.Write(participant.Id);
                writer.Write(participant.DisplayName);
            }
            return str.ToArray();
        }

        public static MatchParticipant ParseJoinRequestPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            str.Seek(HeaderSize, SeekOrigin.Begin);
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
                writer.Write(PartJoinedHeader);
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
            str.Seek(HeaderSize, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                int idInRoom = reader.ReadInt32();
                string id = reader.ReadString();
                string displayName = reader.ReadString();
                return new RoomParticipant(idInRoom, new MatchParticipant(id, displayName));
            }
        }

        public static byte[] CreateParticipantLeftPacket(int participantId)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(PartLeftHeader);
                writer.Write(participantId);
            }
            return str.ToArray();
        }

        public static int ParseParticipantLeftPacket(byte[] packet)
        {
            // Skip PARL header and read ID.
            return BitConverter.ToInt32(packet, HeaderSize);
        }

        public static byte[] CreateRoomPacket(OwnedRoom room)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str, Encoding.UTF8, true))
            {
                // ROOM header
                writer.Write(RoomHeader);
                // GUID
                writer.Write(room.Guid.ToByteArray());
                // Port
                writer.Write(room.Port);
            }
            // Attributes
            WriteAttributes(room.Attributes, str);
            return str.ToArray();
        }

        public static RoomInfo ParseRoomPacket(string sender, byte[] packet, MatchmakingService service)
        {
            var str = new MemoryStream(packet);
            // Skip ROOM header
            str.Seek(HeaderSize, SeekOrigin.Begin);
            Guid id;
            ushort port;
            using (var reader = new BinaryReader(str, Encoding.UTF8, true))
            {
                // GUID
                var guidBytes = reader.ReadBytes(16);
                id = new Guid(guidBytes);

                // Room port.
                port = reader.ReadUInt16();
            }

            // Attributes
            var attributes = ParseAttributes(str);
            return new RoomInfo(service, id, sender, port, attributes, DateTime.UtcNow);
        }

        public static byte[] CreateFindByAttributesPacket(IReadOnlyDictionary<string, object> attributes)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str, Encoding.UTF8, true))
            {
                writer.Write(FindByAttrHeader);
            }
            if (attributes != null)
            {
                WriteAttributes(attributes, str);
            }
            return str.ToArray();
        }

        public static IReadOnlyDictionary<string, object> ParseFindByAttributesPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip header.
            str.Seek(HeaderSize, SeekOrigin.Begin);
            if (str.Position < str.Length)
            {
                return ParseAttributes(str);
            }
            return new Dictionary<string, object>();
        }

        public static byte[] CreateFindByIdPacket(Guid id)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(FindByIdHeader);
                writer.Write(id.ToByteArray());
            }
            return str.ToArray();
        }

        public static Guid ParseFindByIdPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip header.
            str.Seek(HeaderSize, SeekOrigin.Begin);
            // Read GUID.
            var bytes = new byte[16];
            str.Read(bytes, 0, 16);
            return new Guid(bytes);
        }
        public static byte[] CreateFindByOwnerPacket(IMatchParticipant owner)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(FindByOwnerHeader);
                writer.Write(owner.Id);
            }
            return str.ToArray();
        }

        public static string ParseFindByOwnerPacket(byte[] packet)
        {
            var str = new MemoryStream(packet);
            // Skip header.
            str.Seek(HeaderSize, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                return reader.ReadString();
            }
        }

        public static byte[] CreateFindByParticipantsPacket(IEnumerable<IMatchParticipant> participants)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                writer.Write(FindByParticipantsHeader);
                foreach(var p in participants)
                {
                    writer.Write(p.Id);
                }
            }
            return str.ToArray();
        }

        public static IEnumerable<string> ParseFindByParticipantsPacket(byte[] packet)
        {
            var res = new List<string>();
            var str = new MemoryStream(packet);
            // Skip header.
            str.Seek(HeaderSize, SeekOrigin.Begin);
            using (var reader = new BinaryReader(str))
            {
                // Read participant IDs until the end of the packet.
                while (true)
                {
                    try
                    {
                        res.Add(reader.ReadString());
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                }
            }
            return res;
        }
    }
}
#endif