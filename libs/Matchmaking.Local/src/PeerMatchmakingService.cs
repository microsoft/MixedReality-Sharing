// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    // Holds info for an in-progress discovery operation
    class PeerDiscoveryRequest
    {
        internal IReadOnlyDictionary<string, string> query_;
        internal ObservableCollection<IRoom> discovered_ = new ObservableCollection<IRoom>();
        internal ReadOnlyObservableCollection<IRoom> rodiscovered_;

        public PeerDiscoveryRequest(IReadOnlyDictionary<string, string> query)
        {
            rodiscovered_ = new ReadOnlyObservableCollection<IRoom>(discovered_);
            query_ = query;
        }

        internal static bool IsStrictSubset(IReadOnlyDictionary<string, string> query, IReadOnlyDictionary<string, string> attrs)
        {
            foreach (var kvp in query)
            {
                string val;
                if (attrs.TryGetValue(kvp.Key, out val) == false)
                {
                    return false;
                }
                if (kvp.Value != val)
                {
                    return false;
                }
            }
            return true;
        }

        internal bool Match(IRoom room)
        {
            if (query_ == null)
            {
                return true;
            }
            return IsStrictSubset(query_, room.Attributes);
        }
    }

    // Room which has been created locally. And is owned locally.
    class PeerLocalRoom : IRoom
    {
        public PeerLocalRoom(string connection, IReadOnlyDictionary<string, string> attrs)
        {
            Connection = connection;
            Attributes = attrs;
        }

        public string Connection { get; }
        public DateTime LastRefreshed { get { return DateTime.Now; } }
        public IReadOnlyDictionary<string, string> Attributes { get; }
    }

    // Room which we've heard about from a remote
    class PeerRemoteRoom : IRoom
    {
        public PeerRemoteRoom(string connection, IReadOnlyDictionary<string, string> attrs)
        {
            Connection = connection;
            Attributes = attrs;
        }

        public string Connection { get; }
        public DateTime LastRefreshed { get => DateTime.Now; }
        public IReadOnlyDictionary<string, string> Attributes { get; }
    }

    /// <summary>
    /// Simple matchmaking service for local networks.
    /// </summary>
    public class PeerMatchmakingService : IMatchmakingService
    {
        internal static class Proto
        {
            internal const int SrvAnnounce = ('S' << 24) | ('A' << 16) | ('N' << 8) | 'N';
            internal const int SrvQuery = ('S' << 24) | ('Q' << 16) | ('R' << 8) | 'Y';
            internal const int SrvReply = ('S' << 24) | ('R' << 16) | ('P' << 8) | 'L';
        }

        IPeerNetwork foo_;
        List<PeerLocalRoom> localRooms_ = new List<PeerLocalRoom>();
        List<PeerDiscoveryRequest> discovery_ = new List<PeerDiscoveryRequest>();

        public PeerMatchmakingService(IPeerNetwork foo)
        {
            this.foo_ = foo;
            foo_.Message += OnMessage;
            foo_.Start();
        }

        internal void OnMessage(IPeerNetwork comms, IPeerNetworkMessage msg)
        {
            byte[] packet = msg.Message;
            switch (BitConverter.ToInt32(packet, 0))
            {
                case Proto.SrvAnnounce:
                    // TODO - check if it matches an existing query
                    break;
                case Proto.SrvQuery:
                {
                    var ms = new MemoryStream(packet);
                    ms.Position += 4;
                    using (var br = new BinaryReader(ms))
                    {
                        var numAttr = br.ReadInt32();
                        ReadOnlyDictionary<string, string> dict = null;
                        if (numAttr > 0)
                        {
                            var d = new Dictionary<string, string>();
                            for (int i = 0; i < numAttr; ++i)
                            {
                                var k = br.ReadString();
                                var v = br.ReadString();
                                d.Add(k, v);
                            }
                            dict = new ReadOnlyDictionary<string, string>(d);
                        }
                        foreach (var room in localRooms_)
                        {
                            if (dict == null || PeerDiscoveryRequest.IsStrictSubset(dict, room.Attributes))
                            {
                                _Reply(msg, (BinaryWriter w) =>
                                {
                                    w.Write(Proto.SrvReply);
                                    w.Write(room.Connection);
                                    w.Write(room.Attributes.Count);
                                    foreach (var attr in room.Attributes)
                                    {
                                        w.Write(attr.Key);
                                        w.Write(attr.Value.ToString());
                                    }
                                });
                            }
                        }
                    }
                    break;
                }
                case Proto.SrvReply:
                {
                    var ms = new MemoryStream(packet);
                    ms.Position += 4;
                    using (var br = new BinaryReader(ms))
                    {
                        var con = br.ReadString();
                        var cnt = br.ReadInt32();
                        var attrs = cnt != 0 ? new Dictionary<string, string>() : null;
                        for (int i = 0; i < cnt; ++i)
                        {
                            var k = br.ReadString();
                            var v = br.ReadString();
                            attrs.Add(k, v);
                        }
                        var room = new PeerRemoteRoom(con, attrs); // check if duplicate? internal guid?
                        foreach (var disc in discovery_)
                        {
                            if (disc.Match(room))
                            {
                                disc.discovered_.Add(room);
                            }
                        }
                    }
                    break;
                }
            }
        }

        public ReadOnlyObservableCollection<IRoom> StartDiscovery(IReadOnlyDictionary<string, string> query)
        {
            lock (this)
            {
                var disc = new PeerDiscoveryRequest(query);
                discovery_.Add(disc);
                foreach (var r in localRooms_)
                {
                    if (disc.Match(r))
                    {
                        disc.discovered_.Add(r);
                    }
                }

                _Broadcast((BinaryWriter w) =>
                {
                    w.Write(Proto.SrvQuery);
                    if (query != null)
                    {
                        w.Write(query.Count);
                        foreach (var kvp in query)
                        {
                            w.Write(kvp.Key);
                            w.Write(kvp.Value.ToString());
                        }
                    }
                    else
                    {
                        w.Write(0);
                    }
                });

                return new ReadOnlyObservableCollection<IRoom>(disc.discovered_);
            }
        }

        public void StopDiscovery(ReadOnlyObservableCollection<IRoom> rooms)
        {
            lock (this)
            {
                for (int i = 0; i < discovery_.Count; ++i)
                {
                    if (discovery_[i].rodiscovered_ == rooms)
                    {
                        discovery_.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        public Task<IRoom> CreateRoomAsync(
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            var room = new PeerLocalRoom(connection,
                attributes != null ? attributes : new Dictionary<string, string>());
            lock (this)
            {
                localRooms_.Add(room);
                foreach (var d in discovery_) // Update any existing queries immediately
                {
                    if (d.Match(room))
                    {
                        d.discovered_.Add(room);
                    }
                }
            }
            _Broadcast((BinaryWriter w) =>
            {
                w.Write(Proto.SrvAnnounce);
                w.Write(connection);
                w.Write(room.Attributes.Count);
                foreach (var kvp in room.Attributes)
                {
                    w.Write(kvp.Key);
                    w.Write(kvp.Value.ToString()); //TODO handle more types
                }
            });

            return Task<IRoom>.FromResult((IRoom)room);
        }

        public void Dispose()
        {
            //TODO broadcast about the local rooms that we are shutting down
            foo_.Stop();
            foo_ = null;
        }

        internal void _Broadcast(Action<BinaryWriter> cb)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
            }
            foo_.Broadcast(str.ToArray());
        }

        internal void _Reply(IPeerNetworkMessage msg, Action<BinaryWriter> cb)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
            }
            foo_.Reply(msg, str.ToArray());
        }
    }
}
