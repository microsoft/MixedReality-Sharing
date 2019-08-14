using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastOwnedRoom : UDPMulticastRoom, IOwnedRoom
    {
        private volatile IDictionary<string, string> writeableAttributes;
        private CancellationTokenSource hostingCTS = null;
        private Socket hostingSocket = null;

        public ISession Session { get; }

        internal UDPMulticastOwnedRoom(UDPMulticastMatchmakingService matchmakingService, UDPMulticastRoomConfiguration roomConfig, ISession session, IDictionary<string, string> attributes)
            : base(matchmakingService, roomConfig)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));

            owner = matchmakingService.ParticipantProvider.CurrentParticipant;
            writeableAttributes = attributes;
            this.attributes = new ReadOnlyDictionary<string, string>(attributes);

            ETag = DateTime.UtcNow.Ticks;
        }

        internal void StartHosting(CancellationToken cancellationToken)
        {
            hostingCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task.Run(() => HostAsync(hostingCTS.Token), cancellationToken).FireAndForget();
        }

        protected override void OnManagedDispose() // TODO update to using disposablebase
        {
            hostingCTS?.Cancel();
            hostingCTS?.Dispose();
            hostingCTS = null;

            hostingSocket?.Close();
            hostingSocket?.Dispose();
            hostingSocket = null;

            Session.Dispose();
        }

        public void UpdateAttributes(Action<IDictionary<string, string>> updateCallback)
        {
            lock (DisposeLockObject)
            {
                updateCallback(writeableAttributes);
            }
        }

        private async Task HostAsync(CancellationToken cancellationToken)
        {
            try
            {
                hostingSocket = new Socket(RoomConfig.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                hostingSocket.Bind(new IPEndPoint(RoomConfig.Address, RoomConfig.InfoPort));
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
            // Expectation is that we connect to the host at given port, and download the following:
            // [4 bytes : n length] [n-bytes : owner id]
            // [2 bytes : m attributes] can be 0
            // m * [[4 bytes : key length] [key-bytes : attribute key] [4 bytes : val length] [val-bytes : attribute value]]

            using (NetworkStream stream = new NetworkStream(client))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Owner.Id);

                lock (DisposeLockObject)
                {
                    writer.Write((ushort)Attributes.Count);

                    foreach (KeyValuePair<string, string> attribute in Attributes)
                    {
                        writer.Write(attribute.Key);
                        writer.Write(attribute.Value);
                    }
                }

                await stream.FlushAsync();
            }
        }

        public override Task<ISession> JoinAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Can't join a room that is hosted by the current client.");
        }
    }
}
