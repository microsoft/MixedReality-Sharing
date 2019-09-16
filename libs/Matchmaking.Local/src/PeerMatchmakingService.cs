// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    class Proto
    {
        internal const int ServerHello = ('S' << 24) | ('E' << 16) | ('L' << 8) | 'O';
        internal const int ServerByeBye = ('S' << 24) | ('B' << 16) | ('Y' << 8) | 'E';

        internal const int ServerReply = ('S' << 24) | ('R' << 16) | ('P' << 8) | 'L';
        internal const int ClientQuery = ('C' << 24) | ('Q' << 16) | ('R' << 8) | 'Y';

        internal delegate void ServerAnnounceCallback(IPeerNetworkMessage msg, string category, Guid guid, string connection, long expiresFileTime, Dictionary<string, string> attributes);
        internal delegate void ServerByeByeCallback(IPeerNetworkMessage msg, string[] category, Guid[] guid);
        internal delegate void ClientQueryCallback(IPeerNetworkMessage msg, string category);

        internal ServerAnnounceCallback OnServerHello;
        internal ServerByeByeCallback OnServerByeBye;
        internal ServerAnnounceCallback OnServerReply;
        internal ClientQueryCallback OnClientQuery;

        private static void DecodeServerAnnounce(ServerAnnounceCallback callback, IPeerNetworkMessage msg)
        {
            using (var ms = new MemoryStream(msg.Message, 4, msg.Message.Length - 4, false))
            using (var br = new BinaryReader(ms))
            {
                var cat = br.ReadString();
                var uid = new Guid(br.ReadBytes(16));
                var con = br.ReadString();
                var expiresDelta = br.ReadInt32();
                if (expiresDelta < 0)
                {
                    return;
                }
                var expires = DateTime.UtcNow.AddSeconds(expiresDelta).Ticks;
                var cnt = br.ReadInt32();
                var attrs = cnt != 0 ? new Dictionary<string, string>() : null;
                for (int i = 0; i < cnt; ++i)
                {
                    var k = br.ReadString();
                    var v = br.ReadString();
                    attrs.Add(k, v);
                }
                callback(msg, cat, uid, con, expires, attrs);
            }
        }

        private static void DecodeServerByeBye(ServerByeByeCallback callback, IPeerNetworkMessage msg)
        {
            using (var ms = new MemoryStream(msg.Message, 4, msg.Message.Length - 4, false))
            using (var br = new BinaryReader(ms))
            {
                int numRemoved = br.ReadInt32();
                var categories = new string[numRemoved];
                var guids = new Guid[numRemoved];
                for (int i = 0; i < numRemoved; ++i)
                {
                    categories[i] = br.ReadString();
                    guids[i] = new Guid(br.ReadBytes(16));
                }
                callback(msg, categories, guids);
            }
        }

        private static void DecodeClientQuery(ClientQueryCallback callback, IPeerNetworkMessage msg)
        {
            using (var ms = new MemoryStream(msg.Message, 4, msg.Message.Length - 4, false))
            using (var br = new BinaryReader(ms))
            {
                var category = br.ReadString();
                callback(msg, category);
            }
        }

        internal void Dispatch(IPeerNetworkMessage msg)
        {
            if (msg.Message.Length < 4)
            {
                return; // throw
            }
            switch (BitConverter.ToInt32(msg.Message, 0))
            {
                case Proto.ServerHello:
                {
                    if (OnServerHello != null)
                    {
                        DecodeServerAnnounce(OnServerHello, msg);
                    }
                    break;
                }
                case Proto.ServerByeBye:
                {
                    if (OnServerByeBye != null)
                    {
                        DecodeServerByeBye(OnServerByeBye, msg);
                    }
                    break;
                }
                case Proto.ServerReply:
                {
                    if (OnServerReply != null)
                    {
                        DecodeServerAnnounce(OnServerReply, msg);
                    }
                    break;
                }
                case Proto.ClientQuery:
                {
                    if (OnClientQuery != null)
                    {
                        DecodeClientQuery(OnClientQuery, msg);
                    }
                    break;
                }
            }
        }
    }

    class Server
    {
        /// The network for this matchmaking
        IPeerNetwork net_;

        /// The list of all local rooms of all categories
        SortedSet<LocalRoom> localRooms_ = new SortedSet<LocalRoom>(new Extensions.RoomComparer());

        /// Timer for re-announcing rooms.
        Timer timer_;
        /// Time when the timer will fire or MaxValue if the timer is unset.
        DateTime timerExpiryTime = DateTime.MaxValue;

        /// Protocol handler.
        Proto proto_ = new Proto();

        internal Server(IPeerNetwork net)
        {
            proto_.OnClientQuery = OnClientQuery;
            net_ = net;
            net_.Message += ServerOnMessage;
            timer_ = new Timer(OnServerTimerExpired);
        }

        void OnClientQuery(IPeerNetworkMessage msg, string category)
        {
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

        internal void OnServerTimerExpired(object state)
        {
            var now = DateTime.UtcNow;
            var todo = new List<LocalRoom>();
            lock (this)
            {
                foreach (var r in localRooms_)
                {
                    if (r.NextAnnounceTime < now)
                    {
                        r.LastAnnouncedTime = now;
                        todo.Add(r);
                    }
                }
                UpdateAnnounceTimer();
            }
            foreach (var room in todo)
            {
                Extensions.Broadcast(net_, (BinaryWriter w) =>
                {
                    w.Write(Proto.ServerHello);
                    SendAnnounceBody(w, room);
                });
            }
        }


        internal void Stop()
        {
            net_.Message -= ServerOnMessage;
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
        }

        private void UpdateAnnounceTimer()
        {
            Debug.Assert(Monitor.IsEntered(this)); // Caller should have lock(this)
            var next = localRooms_.Min(r => r.NextAnnounceTime);
            if (next != null)
            {
                var now = DateTime.UtcNow;
                var delta = next.Subtract(now);
                timerExpiryTime = next;
                timer_.Change((int)Math.Max(delta.TotalMilliseconds + 1, 0), -1);
            }
            else // no more rooms
            {
                timer_.Change(-1, -1);
                timerExpiryTime = DateTime.MaxValue;
            }
        }

        internal Task<IRoom> CreateRoomAsync(
            string category,
            string connection,
            int expirySeconds,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            var room = new LocalRoom(category, connection, expirySeconds,
                attributes != null ? attributes : new Dictionary<string, string>());
            lock (this)
            {
                localRooms_.Add(room); // new local rooms get a new guid, always unique
                UpdateAnnounceTimer();
            }

            return Task<IRoom>.FromResult((IRoom)room);
        }

        // Room which has been created locally. And is owned locally.
        class LocalRoom : IRoom
        {
            public LocalRoom(string category, string connection, int expirySeconds, IReadOnlyDictionary<string, string> attrs)
            {
                Category = category;
                UniqueId = Guid.NewGuid();
                Connection = connection;
                ExpirySeconds = expirySeconds;
                Attributes = attrs;
            }

            public string Category { get; }
            public Guid UniqueId { get; }
            public int ExpirySeconds { get; } // Relative time. Interval from announce to expiration.
            public DateTime LastAnnouncedTime = DateTime.MinValue; // Absolute FileTime.
            public string Connection { get; }
            public IReadOnlyDictionary<string, string> Attributes { get; }
            public DateTime NextAnnounceTime
            {
                // Reannounce at 45% of expiry time. On an unreliable network, that gives 2 chances
                // for clients to refresh before expiring.
                get => LastAnnouncedTime.AddSeconds(0.45 * ExpirySeconds);
            }
        }

        // Network helpers

        void SendAnnounceBody(BinaryWriter w, LocalRoom room)
        {
            w.Write(room.Category);
            w.Write(room.UniqueId.ToByteArray());
            w.Write(room.Connection);
            w.Write(room.ExpirySeconds);
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
            proto_.Dispatch(msg);
        }
    }

    class Client
    {
        /// The network
        IPeerNetwork net_;

        /// Timer for expiring rooms.
        Timer timer_;
        /// Time when the timer will fire or -1 if the timer is unset.
        long timerExpiryFileTime = -1;

        /// The list of all local rooms of all categories
        IDictionary<string, CategoryInfo> infoFromCategory_ = new Dictionary<string, CategoryInfo>();

        /// Protocol handler.
        Proto proto_ = new Proto();

        internal Client(IPeerNetwork net)
        {
            proto_.OnServerHello = OnServerAnnounce;
            proto_.OnServerByeBye = OnServerByeBye;
            proto_.OnServerReply = OnServerAnnounce;
            timer_ = new Timer(OnClientTimerExpired);
            net_ = net;
            net_.Message += ClientOnMessage;
        }

        internal IDiscoveryTask StartDiscovery(string category)
        {
            lock (this)
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
            public RemoteRoom(string category, Guid uniqueId, string connection, IReadOnlyDictionary<string, string> attrs, long expirationFileTime)
            {
                Category = category;
                UniqueId = uniqueId;
                Connection = connection;
                Attributes = attrs;
                ExpirationFileTime = expirationFileTime;
            }

            public long ExpirationFileTime; // Windows FileTime (100ns ticks since 1601)
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

        private void SetExpirationTimer(long fileTime)
        {
            lock (this)
            {
                var expiryDate = new DateTime(fileTime);
                var deltaMs = (long)DateTime.UtcNow.Subtract(expiryDate).TotalMilliseconds;
                // Round up to the next ms to ensure the (finer grained) fileTime has passed.
                // Also ensure we have a positive delta or the timer will not work.
                deltaMs = Math.Max(deltaMs + 1, 0);
                timer_.Change(deltaMs, Timeout.Infinite);
                timerExpiryFileTime = fileTime;
            }
        }

        private void OnClientTimerExpired(object state)
        {
            var updatedTasks = new List<DiscoveryTask>();
            long nextExpiryFileTime = long.MaxValue;
            lock (this)
            {
                // Search and delete any expired rooms.
                // Also check the next expiry so we can reset the timer.
                DateTime nowDate = DateTime.UtcNow;
                long nowFileTime = nowDate.ToFileTime();
                foreach (var info in infoFromCategory_.Values)
                {
                    var expired = new List<Guid>();
                    foreach (var kvp in info.roomsRemote_)
                    {
                        if (kvp.Value.ExpirationFileTime <= nowFileTime) //room expired?
                        {
                            expired.Add(kvp.Key);
                        }
                        else if (kvp.Value.ExpirationFileTime < nextExpiryFileTime) // room next to expire?
                        {
                            nextExpiryFileTime = kvp.Value.ExpirationFileTime;
                        }
                    }
                    if (expired.Any())
                    {
                        info.roomSerial_ += 1;
                        foreach (var exp in expired)
                        {
                            info.roomsRemote_.Remove(exp);
                        }
                        updatedTasks.AddRange(info.tasks_);
                    }
                }
            }
            // Notify any tasks if we've removed rooms
            foreach (var up in updatedTasks)
            {
                up.FireUpdated();
            }

            if (nextExpiryFileTime != long.MaxValue)
            {
                SetExpirationTimer(nextExpiryFileTime);
            }
        }

        // The body of ServerHello and ServerReply is identical so we reuse the code.
        private void OnServerAnnounce(IPeerNetworkMessage msg, string category, Guid guid, string connection, long expiresFileTime, Dictionary<string, string> attributes)
        {
            DiscoveryTask[] tasksUpdated = null;
            lock (this)
            {
                // see if the category is relevant to us, we created an info in StartDiscovery if so.
                CategoryInfo info;
                if (!infoFromCategory_.TryGetValue(category, out info))
                {
                    return; // we don't care about this category
                }
                RemoteRoom room;
                bool updated = false;
                if (!info.roomsRemote_.TryGetValue(guid, out room)) // new room
                {
                    room = new RemoteRoom(category, guid, connection, attributes, expiresFileTime);
                    info.roomsRemote_[guid] = room;
                    updated = true;
                }
                else // existing room, has it changed?
                {
                    if (room.Category != category)
                    {
                        room.Category = category;
                        updated = true;
                    }
                    if (room.Connection != connection)
                    {
                        room.Connection = connection;
                        updated = true;
                    }
                    if (!Extensions.DictionariesEqual(room.Attributes, attributes))
                    {
                        room.Attributes = attributes;
                        updated = true;
                    }
                    if (room.ExpirationFileTime != expiresFileTime)
                    {
                        room.ExpirationFileTime = expiresFileTime;
                    }
                }
                // If this expiry is sooner than the current timer, we need to reset the timer.
                if (expiresFileTime < timerExpiryFileTime)
                {
                    SetExpirationTimer(expiresFileTime);
                }
                if (updated)
                {
                    info.roomSerial_ += 1;
                    tasksUpdated = info.tasks_.ToArray();
                }
            }
            if (tasksUpdated != null) // outside the lock
            {
                foreach (var t in tasksUpdated)
                {
                    t.FireUpdated();
                }
            }
        }

        private void OnServerByeBye(IPeerNetworkMessage msg, string[] category, Guid[] guid)
        {
            Debug.Assert(category.Length == guid.Length);
            lock (this)
            {
                for (int i = 0; i < category.Length; ++i)
                {
                    CategoryInfo info;
                    if (infoFromCategory_.TryGetValue(category[i], out info))
                    {
                        info.roomsRemote_.Remove(guid[i]);
                        info.roomSerial_ += 1;
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
            Debug.Assert(comms == net_);
            proto_.Dispatch(msg);
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
            return server_.CreateRoomAsync(category, connection, 30/*expiry*/, attributes, token);
        }

        public void Dispose()
        {
            server_?.Stop();
            client_?.Stop();

            // Give some time for the ByeBye message to be sent before shutting down the sockets.
            // todo is there a smarter way to do this?
            Task.Delay(1).Wait();

            network_.Stop();
            network_ = null;
        }
    }
}
