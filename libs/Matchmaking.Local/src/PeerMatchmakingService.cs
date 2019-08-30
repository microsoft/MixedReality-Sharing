// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    internal static class Extensions
    {
        // Return true if (Count(a)<= 1) or (isOk( a[n], a[n+1] ) is true for all n).
        internal static bool CheckAdjacenctElements<T>(IEnumerable<T> a, Func<T, T, bool> isOk)
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

        internal static IEnumerable<T> MergeSortedEnumerables<T>(IEnumerable<T> a, IEnumerable<T> b, Func<T, T, int> compare)
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

        internal static bool DictionariesEqual<K, V>(IReadOnlyDictionary<K, V> a, IDictionary<K, V> b)
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
            else // Deep compare, using the sorted keyvalue pairs.
            {
                // potentially slow for large dictionaries
                var sa = a.OrderBy(kvp => kvp.Key);
                var sb = b.OrderBy(kvp => kvp.Key);
                return sa.SequenceEqual(sb);
            }
        }

        // network helpers

        internal static void Broadcast(IPeerNetwork net, Action<BinaryWriter> cb)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
            }
            net.Broadcast(str.ToArray());
        }

        internal static void Reply(IPeerNetwork net, IPeerNetworkMessage msg, Action<BinaryWriter> cb)
        {
            var str = new MemoryStream();
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
            }
            net.Reply(msg, str.ToArray());
        }

    }

    static class Proto
    {
        internal const int ServerHello = ('S' << 24) | ('E' << 16) | ('L' << 8) | 'O';
        internal const int ServerByeBye = ('S' << 24) | ('B' << 16) | ('Y' << 8) | 'E';

        internal const int ServerReply = ('S' << 24) | ('R' << 16) | ('P' << 8) | 'L';
        internal const int ClientQuery = ('C' << 24) | ('Q' << 16) | ('R' << 8) | 'Y';
    }

    class Server
    {
        /// The network for this matchmaking
        IPeerNetwork net_;

        /// The list of all local rooms of all categories
        SortedSet<LocalRoom> localRooms_ = new SortedSet<LocalRoom>(new Extensions.RoomComparer());

        internal Server(IPeerNetwork net)
        {
            net_ = net;
            net_.Message += ServerOnMessage;
        }

        internal void Stop()
        {
            Extensions.Broadcast(net_, (BinaryWriter w) =>
            {
                w.Write(Proto.ServerByeBye);
                w.Write(localRooms_.Count);
                foreach (var room in localRooms_)
                {
                    w.Write(room.Category);
                    w.Write(room.UniqueId.ToByteArray());
                }
            });
            net_.Message -= ServerOnMessage;
        }

        internal Task<IRoom> CreateRoomAsync(
            string category,
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            var room = new LocalRoom(category, connection,
                attributes != null ? attributes : new Dictionary<string, string>());
            lock (this)
            {
                localRooms_.Add(room); // new local rooms get a new guid, always unique
            }
            Extensions.Broadcast(net_, (BinaryWriter w) =>
            {
                w.Write(Proto.ServerHello);
                SendAnnounceBody(w, room);
            });

            return Task<IRoom>.FromResult((IRoom)room);
        }

        // Room which has been created locally. And is owned locally.
        class LocalRoom : IRoom
        {
            public LocalRoom(string category, string connection, IReadOnlyDictionary<string, string> attrs)
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

        // Network helpers

        void SendAnnounceBody(BinaryWriter w, IRoom room)
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

        // The protocol is a hybrid announce+query which allows for quick response without large amounts
        // of traffic. Servers both broadcast announce messages at a low frequency and also unicast replies
        // directly in response to client queries.
        // Clients broadcast ClientQuery on startup and servers unicast reply with ServerReply.
        // Clients also listen for announce messages.
        // Servers broadcast ServerHello/ServerByeBye on service startup/shutdown respectively.
        // If the underlying transport is lossy, it may choose to send packets multiple times so
        // we need to expect duplicate messages.
        private void ServerOnMessage(IPeerNetwork comms, IPeerNetworkMessage msg)
        {
            byte[] packet = msg.Message;
            switch (BitConverter.ToInt32(packet, 0))
            {
                case Proto.ServerHello:
                case Proto.ServerByeBye:
                case Proto.ServerReply:
                {
                    break;
                }
                case Proto.ClientQuery:
                {
                    var ms = new MemoryStream(packet);
                    ms.Position += 4;
                    using (var br = new BinaryReader(ms))
                    {
                        var category = br.ReadString();
                        LocalRoom[] matching;
                        lock (this)
                        {
                            matching = (from lr in localRooms_
                                        where lr.Category == category
                                        select lr).ToArray();
                        }
                        foreach (var room in matching)
                        {
                            Extensions.Reply(net_, msg, (BinaryWriter w) =>
                            {
                                w.Write(Proto.ServerReply);
                                SendAnnounceBody(w, room);
                            });
                        }
                    }
                    break;
                }
            }
        }
    }

    class Client
    {
        /// The network
        IPeerNetwork net_;

        /// The list of all local rooms of all categories
        IDictionary<string, CategoryInfo> infoFromCategory_ = new Dictionary<string, CategoryInfo>();

        internal Client(IPeerNetwork net)
        {
            net_ = net;
            net_.Message += ClientOnMessage;
        }

        internal IDiscoveryTask StartDiscovery(string category)
        {
            lock(this)
            {
                // Create internals for this category if it doesn't already exist.
                CategoryInfo info;
                if (!infoFromCategory_.TryGetValue(category, out info))
                {
                    info = new CategoryInfo(category);
                    infoFromCategory_.Add(category, info);

                    Extensions.Broadcast(net_, (BinaryWriter w) =>
                    {
                        w.Write(Proto.ClientQuery);
                        w.Write(category);
                    });
                }
                // start a new task in the category
                var res = new DiscoveryTask(this, info);
                info.tasks_.Add(res);
                return res;
            }
        }

        internal void Stop()
        {
            net_.Message -= ClientOnMessage;
        }

        // Room which we've heard about from a remote
        private class RemoteRoom : IRoom
        {
            public RemoteRoom(string category, Guid uniqueId, string connection, IReadOnlyDictionary<string, string> attrs)
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

        // Internal class which holds the latest results for each category.
        private class CategoryInfo
        {
            internal string category_;

            // Tasks from this category (ephemeral).
            internal IList<DiscoveryTask> tasks_ = new List<DiscoveryTask>();

            // Currently known remote rooms. Each time it is updated, we update roomSerial_ also so that tasks can cache efficiently.
            internal IDictionary<Guid, RemoteRoom> roomsRemote_ = new SortedDictionary<Guid, RemoteRoom>();

            // This is incremented on each change to the category
            internal int roomSerial_ = 0;

            internal CategoryInfo(string category)
            {
                category_ = category;
            }
        }

        // User facing interface for an in-progress discovery operation
        private class DiscoveryTask : IDiscoveryTask
        {
            Client client_;
            CategoryInfo info_;
            IRoom[] cachedRooms_ = null;
            int cachedRoomsSerial_ = -1;

            public IEnumerable<IRoom> Rooms
            {
                get
                {
                    var updated = client_.TaskFetchRooms(info_, cachedRoomsSerial_);
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
                client_.TaskDispose(this, info_);
            }

            public DiscoveryTask(Client client, CategoryInfo info)
            {
                client_ = client;
                info_ = info;
            }
        }

        // Task helpers

        // return the new list of rooms or null if the serial hasn't changed.
        private Tuple<int, IRoom[]> TaskFetchRooms(CategoryInfo info, int serial)
        {
            lock (this) // Update the cached copy if it has changed
            {
                if (info.roomSerial_ == serial)
                {
                    return null;
                }
                // need a copy since .Values is a reference
                var rooms = info.roomsRemote_.Values.ToArray<IRoom>();
                return new Tuple<int, IRoom[]>(info.roomSerial_, rooms);
            }
        }

        private void TaskDispose(DiscoveryTask task, CategoryInfo info)
        {
            lock (this)
            {
                info.tasks_.Remove(task);
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

                CategoryInfo updatedInfo = null;
                lock (this)
                {
                    // see if the category is relevant to us, we created a info in StartDiscovery if so.
                    CategoryInfo info;
                    if (!infoFromCategory_.TryGetValue(cat, out info))
                    {
                        return; // we don't care about this category
                    }
                    RemoteRoom room;
                    if (!info.roomsRemote_.TryGetValue(uid, out room)) // new room
                    {
                        room = new RemoteRoom(cat, uid, con, attrs);
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

        private void HandleByeBye(byte[] packet)
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
                        CategoryInfo info;
                        if (infoFromCategory_.TryGetValue(cat, out info))
                        {
                            info.roomsRemote_.Remove(new Guid(uid));
                            info.roomSerial_ += 1;
                        }
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
        private void ClientOnMessage(IPeerNetwork comms, IPeerNetworkMessage msg)
        {
            byte[] packet = msg.Message;
            switch (BitConverter.ToInt32(packet, 0))
            {
                case Proto.ClientQuery:
                {
                    break;
                }
                case Proto.ServerHello:
                {
                    HandleAnnounce(packet);
                    break;
                }
                case Proto.ServerReply:
                {
                    HandleAnnounce(packet);
                    break;
                }
                case Proto.ServerByeBye:
                {
                    HandleByeBye(packet);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Simple matchmaking service for local networks.
    /// </summary>
    public class PeerMatchmakingService : IMatchmakingService
    {
        /// The network for this matchmaking
        IPeerNetwork network_;
        Server server_;
        Client client_;

        public PeerMatchmakingService(IPeerNetwork network)
        {
            this.network_ = network;
			network_.Start();
        }

        // public interface implementations

        public IDiscoveryTask StartDiscovery(string category)
        {
            lock (this)
            {
                if (client_ == null)
                {
                    client_ = new Client(network_);
                }
            }
            return client_.StartDiscovery(category);
        }

        public Task<IRoom> CreateRoomAsync(
            string category,
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            lock (this)
            {
                if (server_ == null)
                {
                    server_ = new Server(network_);
                }
            }
            return server_.CreateRoomAsync(category, connection, attributes, token);
        }

        public void Dispose()
        {
            server_?.Stop();
            client_?.Stop();
            network_.Stop();
            network_ = null;
        }
    }
}
