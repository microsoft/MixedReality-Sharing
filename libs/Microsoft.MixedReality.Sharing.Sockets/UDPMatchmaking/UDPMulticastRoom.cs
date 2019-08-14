using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public class UDPMulticastRoom : DisposableBase, IRoom
    {
        private readonly UDPMulticastMatchmakingService matchmakingService;

        private CancellationTokenSource lastRoomRefreshCTS = null;
        private Task lastRoomRefreshTask = Task.CompletedTask;

        protected volatile IParticipant owner;
        protected volatile IReadOnlyDictionary<string, string> attributes;

        public long ETag { get; protected set; }

        public string Id { get; }

        public IParticipant Owner => owner;

        public IReadOnlyDictionary<string, string> Attributes => attributes;

        internal UDPMulticastRoomConfiguration RoomConfig { get; }

        internal UDPMulticastRoom(UDPMulticastMatchmakingService matchmakingService, UDPMulticastRoomConfiguration roomConfig)
        {
            Id = roomConfig.Id;
            RoomConfig = roomConfig;
            this.matchmakingService = matchmakingService;
        }

        internal Task RefreshRoomInfoAsync(long newEtag, CancellationToken cancellationToken)
        {
            lock (DisposeLockObject)
            {
                if (newEtag <= ETag)
                {
                    return Task.CompletedTask;
                }

                ETag = newEtag;

                lastRoomRefreshCTS?.Cancel();
                lastRoomRefreshCTS?.Dispose();
                lastRoomRefreshCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                return lastRoomRefreshTask = OnRefreshRoomInfoAsync(lastRoomRefreshTask, cancellationToken);
            }
        }

        private async Task OnRefreshRoomInfoAsync(Task previousTaskToAwait, CancellationToken cancellationToken)
        {
            await previousTaskToAwait.IgnoreCancellation().Unless(cancellationToken);

            try
            {
                // Expectation is that we connect to the host at given port, and download the following:
                // [4 bytes : n length] [n-bytes : owner id]
                // [2 bytes : m attributes] can be 0
                // m * [[4 bytes : key length] [key-bytes : attribute key] [4 bytes : val length] [val-bytes : attribute value]]
                using (Socket socket = new Socket(RoomConfig.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    await socket.ConnectAsync(RoomConfig.Address, RoomConfig.InfoPort);

                    using (BinaryReader reader = new BinaryReader(new NetworkStream(socket), Encoding.ASCII))
                    {
                        string ownerId = reader.ReadString();

                        int numAttributes = reader.ReadUInt16();

                        Dictionary<string, string> newAttriubtes = new Dictionary<string, string>(numAttributes);

                        for (int i = 0; i < numAttributes; i++)
                        {
                            newAttriubtes.Add(reader.ReadString(), reader.ReadString());
                        }


                        // Update with new values
                        owner = await matchmakingService.ParticipantProvider.GetParticipantAsync(ownerId, cancellationToken);
                        attributes = new ReadOnlyDictionary<string, string>(newAttriubtes);
                    }
                }
            }
            catch (EndOfStreamException ex)
            {
                matchmakingService.Logger.LogError($"{nameof(UDPMulticastRoom)}.{nameof(RefreshRoomInfoAsync)}: Downloading malformed room info data, reached end of stream before completing reading.", ex);
            }
        }

        public virtual async Task<ISession> JoinAsync(CancellationToken cancellationToken)
        {
            return await matchmakingService.SessionFactory.JoinSessionAsync(RoomConfig, attributes, cancellationToken);
        }
    }
}
