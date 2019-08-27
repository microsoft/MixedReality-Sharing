// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public static class Extensions
    {
        public static IEnumerable<T> MergeSortedEnumerators<T>(IEnumerator<T> a, IEnumerator<T> b, Func<T, T, bool> less)
        {
            // a and b have at least 1 element
            while (true)
            {
                if (less(a.Current, b.Current))
                {
                    yield return a.Current;
                    if (a.MoveNext() == false)
                    {
                        do
                        {
                            yield return b.Current;
                        } while (b.MoveNext());
                        yield break;
                    }
                }
                else
                {
                    yield return b.Current;
                    if (b.MoveNext() == false)
                    {
                        do
                        {
                            yield return a.Current;
                        } while (a.MoveNext());
                        yield break;
                    }
                }
            }
        }

        public static IEnumerable<T> MergeSortedEnumerables<T>(IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> less)
        {
            var ea = a.GetEnumerator();
            var eb = b.GetEnumerator();
            // early outs for empty lists
            if (ea.MoveNext() == false)
            {
                return b;
            }
            if (eb.MoveNext() == false)
            {
                return a;
            }
            return MergeSortedEnumerators(ea, eb, less);
        }
    }

    // Internal class which holds the latest results for each category.
    internal class PeerCategoryInfo
    {
        internal string category_;

        // Tasks from this category (ephemeral).
        internal IList<PeerDiscoveryTask> tasks_ = new List<PeerDiscoveryTask>();

        // Currently known remote rooms. Each time it is updated, we update roomSerial_ also so that tasks can cache efficiently.
        internal IDictionary<Guid, PeerRemoteRoom> roomsRemote_ = new SortedDictionary<Guid, PeerRemoteRoom>();

        // This is incremented on each change to the category
        internal int roomSerial_ = 0;

        internal PeerCategoryInfo(string category)
        {
            category_ = category;
        }
    }

    // User facing interface for an in-progress discovery operation
    internal class PeerDiscoveryTask : IDiscoveryTask
    {
        PeerMatchmakingService service_;
        PeerCategoryInfo info_;
        IList<IRoom> cachedRooms_ = null;
        int cachedRoomsSerial_ = -1;

        public IList<IRoom> Rooms
        {
            get
            {
                service_.TaskUpdateRooms(info_, ref cachedRooms_, ref cachedRoomsSerial_);
                return cachedRooms_;
            }
        }

        public event Action<IDiscoveryTask> Updated;

        public void FireUpdated()
        {
            if (Updated != null)
            {
                Updated.Invoke(this);
            }
        }

        public void Dispose()
        {
            service_.TaskDispose(this, info_);
        }

        public PeerDiscoveryTask(PeerMatchmakingService service, PeerCategoryInfo info)
        {
            service_ = service;
            info_ = info;
        }
    }

    // Room which has been created locally. And is owned locally.
    internal class PeerLocalRoom : IRoom
    {
        public PeerLocalRoom(string category, string connection, IReadOnlyDictionary<string, string> attrs)
        {
            Category = category;
            UniqueId = Guid.NewGuid();
            Connection = connection;
            Attributes = attrs;
        }

        public string Category { get; }
        public Guid UniqueId { get; }
        public string Connection { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }
    }

    // Room which we've heard about from a remote
    internal class PeerRemoteRoom : IRoom
    {
        public PeerRemoteRoom(string category, Guid uniqueId, string connection, IReadOnlyDictionary<string, string> attrs)
        {
            Category = category;
            UniqueId = uniqueId;
            Connection = connection;
            Attributes = attrs;
        }

        public string Category { get; set; }
        public Guid UniqueId { get; }
        public string Connection { get; set; }
        public IReadOnlyDictionary<string, string> Attributes { get; set; }
    }

    /// <summary>
    /// Simple matchmaking service for local networks.
    /// </summary>
    public class PeerMatchmakingService : IMatchmakingService
    {
        internal static class Proto
        {
            internal const int ServerHello = ('S' << 24) | ('E' << 16) | ('L' << 8) | 'O';
            internal const int ServerByeBye = ('S' << 24) | ('B' << 16) | ('Y' << 8) | 'E';

            internal const int ServerReply = ('S' << 24) | ('R' << 16) | ('P' << 8) | 'L';
            internal const int ClientQuery = ('C' << 24) | ('Q' << 16) | ('R' << 8) | 'Y';
        }

        /// The network for this matchmaking
        IPeerNetwork network_;
        /// The list of all local rooms of all categories
        internal List<PeerLocalRoom> localRooms_ = new List<PeerLocalRoom>();
        /// The list of all local rooms of all categories
        IDictionary<string, PeerCategoryInfo> infoFromCategory_ = new Dictionary<string, PeerCategoryInfo>();

        public PeerMatchmakingService(IPeerNetwork network)
        {
            this.network_ = network;
            network_.Message += OnMessage;
            network_.Start();
        }

        // Task helpers

        internal void TaskUpdateRooms(PeerCategoryInfo info, ref IList<IRoom> rooms, ref int serial)
        {
            lock (this) // Update the cached copy if it has changed
            {
                if (info.roomSerial_ != serial)
                {
                    var remotes = info.roomsRemote_.Values;
                    var locals = localRooms_.Where(r => r.Category == info.category_);
                    rooms = new List<IRoom>(Extensions.MergeSortedEnumerables<IRoom>(remotes, locals, (a, b) => a.UniqueId.CompareTo(b.UniqueId) <= 0));
                    serial = info.roomSerial_;
                }
            }
        }

        internal void TaskDispose(PeerDiscoveryTask task, PeerCategoryInfo info)
        {
            lock (this)
            {
                info.tasks_.Remove(task);
            }
        }

        // Network helpers

        private void SendAnnounceBody(BinaryWriter w, IRoom room)
        {
            w.Write(room.Category);
            w.Write(room.UniqueId.ToByteArray());
            w.Write(room.Connection);
            w.Write(room.Attributes.Count);
            foreach (var kvp in room.Attributes)
            {
                w.Write(kvp.Key);
                w.Write(kvp.Value);
            }
        }

        // The body of ServerHello and ServerReply is identical so we reuse the code.
        private void HandleAnnounce(byte[] packet)
        {
            var ms = new MemoryStream(packet);
            ms.Position += 4;
            using (var br = new BinaryReader(ms))
            {
                // deserialize packet
                var cat = br.ReadString();
                var uid = new Guid(br.ReadBytes(16));
                var con = br.ReadString();
                var cnt = br.ReadInt32();
                var attrs = cnt != 0 ? new Dictionary<string, string>() : null;
                for (int i = 0; i < cnt; ++i)
                {
                    var k = br.ReadString();
                    var v = br.ReadString();
                    attrs.Add(k, v);
                }

                PeerCategoryInfo updatedInfo = null;
                lock (this)
                {
                    // see if the category is relevant to us, we created a info in StartDiscovery if so.
                    PeerCategoryInfo info;
                    if (infoFromCategory_.TryGetValue(cat, out info) == false)
                    {
                        return; // we don't care about this category
                    }
                    PeerRemoteRoom room;
                    if (info.roomsRemote_.TryGetValue(uid, out room) == false) // new room
                    {
                        room = new PeerRemoteRoom(cat, uid, con, attrs);
                        info.roomsRemote_[uid] = room;
                        info.roomSerial_ += 1;
                        updatedInfo = info;
                    }
                    else // existing room, has it changed?
                    {
                        if (room.Category != cat)
                        {
                            room.Category = cat;
                            updatedInfo = info;
                        }
                        if (room.Connection != con)
                        {
                            room.Connection = con;
                            updatedInfo = info;
                        }
                        if (room.Attributes.OrderBy(kvp => kvp.Key)
                            .SequenceEqual(attrs.OrderBy(kvp => kvp.Key)) == false)
                        {
                            room.Attributes = attrs;
                            updatedInfo = info;
                        }
                    }
                }
                if (updatedInfo != null) // outside the lock
                {
                    foreach (var t in updatedInfo.tasks_)
                    {
                        t.FireUpdated();
                    }
                }
            }
        }
        // The protocol is a hybrid announce+query which allows for quick response without large amounts
        // of traffic. Servers both broadcast announce messages at a low frequency and also unicast replies
        // directly in response to client queries.
        // Clients broadcast ClientQuery on startup and servers unicast reply with ServerReply.
        // Clients also listen for announce messages.
        // Servers broadcast ServerHello/ServerByeBye on service startup/shutdown respectively.
        // If the underlying transport is lossy, it may choose to send packets multiple times so
        // we need to expect duplicate messages.
        private void OnMessage(IPeerNetwork comms, IPeerNetworkMessage msg)
        {
            byte[] packet = msg.Message;
            switch (BitConverter.ToInt32(packet, 0))
            {
                case Proto.ServerHello:
                {
                    HandleAnnounce(packet);
                    break;
                }
                case Proto.ServerByeBye:
                {
                    // TODO - check if it matches an existing query
                    break;
                }
                case Proto.ClientQuery:
                {
                    var ms = new MemoryStream(packet);
                    ms.Position += 4;
                    using (var br = new BinaryReader(ms))
                    {
                        var category = br.ReadString();
                        IList<PeerLocalRoom> matching;
                        lock(this)
                        {
                            matching = localRooms_.FindAll(r => r.Category == category);
                        }
                        foreach (var room in matching)
                        {
                            _Reply(msg, (BinaryWriter w) =>
                            {
                                w.Write(Proto.ServerReply);
                                SendAnnounceBody(w, room);
                            });
                        }
                    }
                    break;
                }
                case Proto.ServerReply:
                {
                    HandleAnnounce(packet);
                    break;
                }
            }
        }

        // public interface implementations

        public IDiscoveryTask StartDiscovery(string category)
        {
            lock (this)
            {
                // Create internals for this category if it doesn't already exist.
                PeerCategoryInfo info;
                if (infoFromCategory_.TryGetValue(category, out info) == false)
                {
                    info = new PeerCategoryInfo(category);
                    infoFromCategory_.Add(category, info);

                    _Broadcast((BinaryWriter w) =>
                    {
                        w.Write(Proto.ClientQuery);
                        w.Write(category);
                    });
                }
                // start a new task in the category
                var res = new PeerDiscoveryTask(this, info);
                info.tasks_.Add(res);
                return res;
            }
        }

        public Task<IRoom> CreateRoomAsync(
            string category,
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            var room = new PeerLocalRoom(category, connection,
                    attributes != null ? attributes : new Dictionary<string, string>());
            lock (this)
            {
                localRooms_.Add(room); // new local rooms get a new guid, always unique
                PeerCategoryInfo pcg;
                // does the new room match an existing query?
                if (infoFromCategory_.TryGetValue(category, out pcg))
                {
                    pcg.roomSerial_ += 1;
                    foreach (var r in pcg.tasks_)
                    {
                        r.FireUpdated();
                    }
                }
            }
            _Broadcast((BinaryWriter w) =>
            {
                w.Write(Proto.ServerHello);
                SendAnnounceBody(w, room);
            });

            return Task<IRoom>.FromResult((IRoom)room);
        }

        public void Dispose()
        {
            _Broadcast((BinaryWriter w) =>
            {
                w.Write(Proto.ServerByeBye);
                w.Write(localRooms_.Count);
                foreach (var room in localRooms_)
                {
                    w.Write(room.UniqueId.ToByteArray());
                }
            });
            network_.Stop();
            network_ = null;
        }

        // network helpers

        internal void _Broadcast(Action<BinaryWriter> cb)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
            }
            network_.Broadcast(str.ToArray());
        }

        internal void _Reply(IPeerNetworkMessage msg, Action<BinaryWriter> cb)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
            }
            network_.Reply(msg, str.ToArray());
        }
    }
}
