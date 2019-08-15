using Microsoft.MixedReality.Sharing.Matchmaking;
using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    internal enum UDPMulticastRoomState
    {
        None,
        Updating,
        Ready,
        Error
    }

    public class UDPMulticastRoom : DisposableBase, IRoom
    {
        private readonly UDPMulticastMatchmakingService matchmakingService;
        private readonly string id;

        private CancellationTokenSource lastRoomRefreshCTS = null;
        private Task<bool> lastRoomRefreshTask = Task.FromResult(false);

        protected volatile IParticipant owner;
        protected volatile IReadOnlyDictionary<string, string> attributes;

        public long ETag { get; protected set; }

        public Guid Id { get; }

        string IRoom.Id => id;

        public IParticipant Owner => owner;

        public IReadOnlyDictionary<string, string> Attributes => attributes;

        internal UDPMulticastRoomConfiguration RoomConfig { get; }

        internal UDPMulticastRoomState State { get; private set; } = UDPMulticastRoomState.None;

        internal UDPMulticastRoom(UDPMulticastMatchmakingService matchmakingService, UDPMulticastRoomConfiguration roomConfig)
        {
            Id = roomConfig.Id;
            id = ((IRoomConfiguration)roomConfig).Id;

            RoomConfig = roomConfig;
            this.matchmakingService = matchmakingService;
        }

        internal Task<bool> RefreshRoomInfoAsync(long newEtag, CancellationToken cancellationToken)
        {
            lock (DisposeLockObject)
            {
                if (newEtag < ETag || (newEtag == ETag && State != UDPMulticastRoomState.Error))
                {
                    return Task.FromResult(false);
                }

                ETag = newEtag;

                lastRoomRefreshCTS?.Cancel();
                lastRoomRefreshCTS?.Dispose();
                lastRoomRefreshCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                State = UDPMulticastRoomState.Updating;

                return lastRoomRefreshTask = OnRefreshRoomInfoAsync(lastRoomRefreshTask, cancellationToken);
            }
        }

        private async Task<bool> OnRefreshRoomInfoAsync(Task previousTaskToAwait, CancellationToken cancellationToken)
        {
            await previousTaskToAwait.IgnoreCancellation().Unless(cancellationToken);

            try
            {
                // Expectation is that we connect to the host at given port, and download the following:
                // [4 bytes : n length] [n-bytes : owner id]
                // [2 bytes : m attributes] can be 0
                // m * [[4 bytes : key length] [key-bytes : attribute key] [4 bytes : val length] [val-bytes : attribute value]]
                using (Socket socket = new Socket(RoomConfig.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                using (cancellationToken.Register(() => socket.Close()))
                {
                    await socket.ConnectAsync(RoomConfig.Address, RoomConfig.InfoPort).IgnoreSocketAbort();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }

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

                lock (DisposeLockObject)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        // If we reached here and aren't cancelled (no new update task was scheduled, then we are ready)
                        State = UDPMulticastRoomState.Ready;
                        return true;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                matchmakingService.Logger.LogError($"{nameof(UDPMulticastRoom)}.{nameof(RefreshRoomInfoAsync)}: Failed to download an update, encountered an exception.", ex);
                lock (DisposeLockObject)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        State = UDPMulticastRoomState.Error;
                    }
                }
            }

            return false;
        }

        public virtual async Task<ISession> JoinAsync(CancellationToken cancellationToken)
        {
            return await matchmakingService.SessionFactory.JoinSessionAsync(RoomConfig, attributes, cancellationToken);
        }
    }
}
