using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.Sockets.UDPMatchmaking.Messages
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AnnounceMessage
    {
        public static int Size { get; } = Marshal.SizeOf<AnnounceMessage>();

        public static byte MessageTypeId { get; } = 1;

        public Guid Id { get; set; }

        public long ETag { get; set; }

        public ushort DataPort { get; set; }

        public ushort InfoPort { get; set; }

        public AnnounceMessage(Guid id, long eTag, ushort dataPort, ushort infoPort)
        {
            Id = id;
            ETag = eTag;
            DataPort = dataPort;
            InfoPort = infoPort;
        }
    }
}
