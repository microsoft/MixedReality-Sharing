using Microsoft.MixedReality.Sharing.Matchmaking;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastRoom : RoomBase
    {
        private readonly UDPMulticastMatchmakingService matchmakingService;

        internal UDPMulticastRoomConfiguration RoomConfig { get; }
        protected readonly IPAddress hostAddress;

        internal UDPMulticastRoom(UDPMulticastMatchmakingService matchmakingService, UDPMulticastRoomConfiguration roomConfig, IPAddress hostAddress)
            : base(roomConfig.Id)
        {
            RoomConfig = roomConfig;
            this.matchmakingService = matchmakingService;
            this.hostAddress = hostAddress;
        }

        internal async Task RefreshRoomInfoAsync(CancellationToken cancellationToken)
        {
            List<Task<IParticipant>> newParticipants;
            Dictionary<string, string> newAttriubtes;
            try
            {
                // Expectation is that we connect to the host at given port, and download the following:
                // [2 bytes : n participants] must be > 1
                // [2 bytes : m attributes] can be 0
                // n * [[4 bytes : y length] [y-bytes : participant id]]
                // m * [[4 bytes : key length] [key-bytes : attribute key] [4 bytes : val length] [val-bytes : attribute value]]
                // Where the first participant is owner
                using (Socket socket = new Socket(hostAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    await socket.ConnectAsync(hostAddress, RoomConfig.InfoPort);

                    using (BinaryReader reader = new BinaryReader(new NetworkStream(socket), Encoding.ASCII))
                    {

                        int numParticipants = reader.ReadUInt16();
                        if (numParticipants == 0)
                        {
                            matchmakingService.Logger.LogError($"{nameof(UDPMulticastRoom)}.{nameof(RefreshRoomInfoAsync)}: Downloading malformed room info data, no participants.");
                            return;
                        }

                        int numAttributes = reader.ReadUInt16();

                        newParticipants = new List<Task<IParticipant>>(numParticipants);
                        newAttriubtes = new Dictionary<string, string>(numAttributes);

                        for (int i = 0; i < numParticipants; i++)
                        {
                            newParticipants.Add(matchmakingService.ParticipantProvider.GetParticipantAsync(reader.ReadString(), cancellationToken));
                        }

                        for (int i = 0; i < numAttributes; i++)
                        {
                            newAttriubtes.Add(reader.ReadString(), reader.ReadString());
                        }
                    }
                }
            }
            catch (EndOfStreamException ex)
            {
                matchmakingService.Logger.LogError($"{nameof(UDPMulticastRoom)}.{nameof(RefreshRoomInfoAsync)}: Downloading malformed room info data, reached end of stream before completing reading.", ex);
                return;
            }

            UpdateAttributes(newAttriubtes);

            IParticipant[] participants = await Task.WhenAll(newParticipants);
            UpdateParticipants(participants[0], participants);
        }
    }
}
