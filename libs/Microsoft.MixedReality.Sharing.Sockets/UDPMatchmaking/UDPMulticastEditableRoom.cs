using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastEditableRoom : UDPMulticastRoom, IEditableRoom
    {
        private CancellationTokenSource hostingCTS = null;
        private Socket hostingSocket = null;

        internal UDPMulticastEditableRoom(UDPMulticastMatchmakingService matchmakingService, UDPMulticastRoomConfiguration roomConfig, IPAddress hostAddress, IDictionary<string, string> attributes)
            : base(matchmakingService, roomConfig, hostAddress)
        {
            UpdateParticipants(matchmakingService.ParticipantProvider.CurrentParticipant, new IParticipant[] { matchmakingService.ParticipantProvider.CurrentParticipant });
            UpdateAttributes(attributes);
        }

        internal void StartHosting(CancellationToken cancellationToken)
        {
            hostingCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(() => HostAsync(hostingCTS.Token), cancellationToken).FireAndForget();
        }

        public void Close()
        {
            hostingCTS?.Cancel();
            hostingCTS?.Dispose();
            hostingCTS = null;

            hostingSocket?.Close();
            hostingSocket?.Dispose();
            hostingSocket = null;
        }

        private async Task HostAsync(CancellationToken cancellationToken)
        {
            try
            {
                hostingSocket = new Socket(hostAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                hostingSocket.Bind(new IPEndPoint(hostAddress, RoomConfig.InfoPort));
                hostingSocket.Listen(100);

                while (!cancellationToken.IsCancellationRequested)
                {
                    Socket client = await hostingSocket.AcceptAsync();
                    Task.Run(() => SendDataAsync(client, cancellationToken), cancellationToken).FireAndForget();
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.OperationAborted)
                {
                    throw;
                }
            }
            catch (ObjectDisposedException) { }
        }

        private async Task SendDataAsync(Socket client, CancellationToken cancellationToken)
        {
            // Expectation is that when someone connect to us - the host, we stream the following:
            // [2 bytes : n participants] must be > 1
            // [2 bytes : m attributes] can be 0
            // n * [[4 bytes : y length] [y-bytes : participant id]]
            // m * [[4 bytes : key length] [key-bytes : attribute key] [4 bytes : val length] [val-bytes : attribute value]]
            // Where the first participant is owner

            using (NetworkStream stream = new NetworkStream(client))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                lock (lockObject)
                {
                    writer.Write((ushort)Participants.Count);
                    writer.Write((ushort)Attributes.Count);

                    foreach (IParticipant participant in Participants)
                    {
                        writer.Write(participant.Id);
                    }

                    foreach (KeyValuePair<string, string> attribute in Attributes)
                    {
                        writer.Write(attribute.Key);
                        writer.Write(attribute.Value);
                    }
                }

                await stream.FlushAsync();
            }
        }
    }
}
