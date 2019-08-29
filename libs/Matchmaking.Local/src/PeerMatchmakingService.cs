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
        // Return true if (Count(a)<= 1) or (isOk( a[n], a[n+1] ) is true for all n).
        public static bool CheckAdjacenctElements<T>(IEnumerable<T> a, Func<T, T, bool> isOk)
        {
            var ea = a.GetEnumerator();
            if (ea.MoveNext())
            {
                var prev = ea.Current;
                while (ea.MoveNext())
                {
                    var cur = ea.Current;
                    if (isOk(prev, cur))
                    {
                        prev = cur;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal class RoomComparer : IComparer<IRoom>
        {
            public int Compare(IRoom a, IRoom b)
            {
                return a.UniqueId.CompareTo(b.UniqueId);
            }
        }

        // Helper method for MergeSortedEnumerables. Assumes "a" and "b" have had MoveNext already called.
        private static IEnumerable<T> MergeSortedEnumerators<T>(IEnumerator<T> a, IEnumerator<T> b, Func<T, T, int> compare)
        {
            // a and b have at least 1 element
            bool moreA = true;
            bool moreB = true;
            while (moreA && moreB)
            {
                switch (compare(a.Current, b.Current))
                {
                    case -1:
                    {
                        yield return a.Current;
                        moreA = a.MoveNext();
                        break;
                    }
                    case 1:
                    {
                        yield return b.Current;
                        moreB = b.MoveNext();
                        break;
                    }
                    case 0: // merge duplicates
                    {
                        yield return a.Current;
                        moreA = a.MoveNext();
                        moreB = b.MoveNext();
                        break;
                    }
                }
            }
            while (moreA)
            {
                yield return a.Current;
                moreA = a.MoveNext();
            }
            while (moreB)
            {
                yield return b.Current;
                moreB = b.MoveNext();
            }
        }

        public static IEnumerable<T> MergeSortedEnumerables<T>(IEnumerable<T> a, IEnumerable<T> b, Func<T, T, int> compare)
        {
            var ea = a.GetEnumerator();
            var eb = b.GetEnumerator();
            // early outs for empty lists
            if (!ea.MoveNext())
            {
                return b;
            }
            if (!eb.MoveNext())
            {
                return a;
            }
            return MergeSortedEnumerators(ea, eb, compare);
        }

        public static bool DictionariesEqual<K, V>(IReadOnlyDictionary<K, V> a, IDictionary<K, V> b)
        {
            if (a == b) // same object or both null
            {
                return true;
            }
            else if (b == null || a == null) // only one null
            {
                return false;
            }
            else if (a.Count != b.Count)
            {
                return false;
            }
            else // Deep compare, using the sorted keyvalue pairs
            {
                var sa = a.OrderBy(kvp => kvp.Key);
                var sb = b.OrderBy(kvp => kvp.Key);
                return sa.SequenceEqual(sb);
            }
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
        List<IRoom> cachedRooms_ = null;
        int cachedRoomsSerial_ = -1;

        public IEnumerable<IRoom> Rooms
        {
            get
            {
                var updated = service_.TaskFetchRooms(info_, cachedRoomsSerial_);
                if (updated != null)
                {
                    cachedRoomsSerial_ = updated.Item1;
                    cachedRooms_ = updated.Item2;
                }
                return cachedRooms_;
            }
        }

        public event Action<IDiscoveryTask> Updated;

        public void FireUpdated()
        {
            Updated?.Invoke(this);
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
        internal SortedSet<PeerLocalRoom> localRooms_ = new SortedSet<PeerLocalRoom>(new Extensions.RoomComparer());
        /// The list of all local rooms of all categories
        IDictionary<string, PeerCategoryInfo> infoFromCategory_ = new Dictionary<string, PeerCategoryInfo>();

        public PeerMatchmakingService(IPeerNetwork network)
        {
            this.network_ = network;
            network_.Message += OnMessage;
            network_.Start();
        }

        // Task helpers

        // return the new list of rooms or null if the serial hasn't changed.
        internal Tuple<int, List<IRoom>> TaskFetchRooms(PeerCategoryInfo info, int serial)
        {
            lock (this) // Update the cached copy if it has changed
            {
                if (info.roomSerial_ == serial)
                {
                    return null;
                }
                var remotes = info.roomsRemote_.Values;
                var locals = localRooms_.Where(r => r.Category == info.category_);
                Debug.Assert(Extensions.CheckAdjacenctElements(locals, (a, b) => a.UniqueId.CompareTo(b.UniqueId) < 0));
                var lst = new List<IRoom>(Extensions.MergeSortedEnumerables<IRoom>(locals, remotes, (a, b) => a.UniqueId.CompareTo(b.UniqueId)));
                return new Tuple<int, List<IRoom>>(info.roomSerial_, lst);
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
                    if (!infoFromCategory_.TryGetValue(cat, out info))
                    {
                        return; // we don't care about this category
                    }
                    PeerRemoteRoom room;
                    if (!info.roomsRemote_.TryGetValue(uid, out room)) // new room
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
                        if (!Extensions.DictionariesEqual(room.Attributes, attrs))
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
                    var ms = new MemoryStream(packet);
                    ms.Position += 4;
                    using (var br = new BinaryReader(ms))
                    {
                        int numRemoved = br.ReadInt32();
                        lock (this)
                        {
                            for (int i = 0; i < numRemoved; ++i)
                            {
                                string cat = br.ReadString();
                                byte[] uid = br.ReadBytes(16);
                                PeerCategoryInfo info;
                                if (infoFromCategory_.TryGetValue(cat, out info))
                                {
                                    info.roomsRemote_.Remove(new Guid(uid));
                                    info.roomSerial_ += 1;
                                }
                            }
                        }
                    }
                    break;
                }
                case Proto.ClientQuery:
                {
                    var ms = new MemoryStream(packet);
                    ms.Position += 4;
                    using (var br = new BinaryReader(ms))
                    {
                        var category = br.ReadString();
                        PeerLocalRoom[] matching;
                        lock (this)
                        {
                            matching = (from lr in localRooms_
                                        where lr.Category == category
                                        select lr).ToArray();
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
                if (!infoFromCategory_.TryGetValue(category, out info))
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
                    w.Write(room.Category);
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
