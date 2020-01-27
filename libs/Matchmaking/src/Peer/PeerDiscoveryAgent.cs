﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
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
        internal class ResourceComparer : IComparer<IDiscoveryResource>
        {
            public int Compare(IDiscoveryResource a, IDiscoveryResource b)
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

        // transport helpers

        internal static void Broadcast(IPeerDiscoveryTransport net, Guid streamId, Action<BinaryWriter> cb)
        {
            byte[] buffer = new byte[1024];
            using (var str = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
                writer.Flush();
                net.Broadcast(streamId, new ArraySegment<byte>(buffer, 0, (int)str.Position));
            }
        }

        internal static void Reply(IPeerDiscoveryTransport net, IPeerDiscoveryMessage msg, Guid streamId, Action<BinaryWriter> cb)
        {
            byte[] buffer = new byte[1024];
            using (var str = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(str))
            {
                cb.Invoke(writer);
                writer.Flush();
                net.Reply(msg, streamId, new ArraySegment<byte>(buffer, 0, (int)str.Position));
            }
        }
    }

    // The protocol is a hybrid announce+query which allows for quick response without large amounts
    // of traffic. Servers both broadcast announce messages at a low frequency and also unicast replies
    // directly in response to client queries.
    // Clients broadcast ClientQuery on startup and servers unicast reply with ServerReply.
    // Clients also listen for announce messages.
    // Servers broadcast ServerHello/ServerByeBye on startup/shutdown respectively.
    // If the underlying transport is lossy, it may choose to send packets multiple times so
    // we need to expect duplicate messages.
    class Proto
    {
        private const int ServerHello = ('S' << 24) | ('E' << 16) | ('L' << 8) | 'O';
        private const int ServerByeBye = ('S' << 24) | ('B' << 16) | ('Y' << 8) | 'E';
        private const int ServerReply = ('S' << 24) | ('R' << 16) | ('P' << 8) | 'L';
        private const int ClientQuery = ('C' << 24) | ('Q' << 16) | ('R' << 8) | 'Y';
        private const int MaxNumAttrs = 1024;

        internal delegate void ServerAnnounceCallback(IPeerDiscoveryMessage msg, string category, string connection, DateTime expiresTime, Dictionary<string, string> attributes);
        internal delegate void ServerByeByeCallback(IPeerDiscoveryMessage msg);
        internal delegate void ClientQueryCallback(IPeerDiscoveryMessage msg, string category);

        IPeerDiscoveryTransport transport_;
        internal ServerAnnounceCallback OnServerHello;
        internal ServerByeByeCallback OnServerByeBye;
        internal ServerAnnounceCallback OnServerReply;
        internal ClientQueryCallback OnClientQuery;

        internal Proto(IPeerDiscoveryTransport net)
        {
            transport_ = net;
            Start();
        }

        internal void Start()
        {
            transport_.Message += OnMessage;
        }

        internal void Stop()
        {
            transport_.Message -= OnMessage;
        }

        // Receiving

        private void OnMessage(IPeerDiscoveryTransport net, IPeerDiscoveryMessage msg)
        {
            Debug.Assert(net == transport_);
            Dispatch(msg);
        }

        private static void DecodeServerAnnounce(ServerAnnounceCallback callback, IPeerDiscoveryMessage msg)
        {
            using (var ms = new MemoryStream(msg.Contents.Array, msg.Contents.Offset + 4, msg.Contents.Count - 4, false))
            using (var br = new BinaryReader(ms))
            {
                var cat = br.ReadString();
                var con = br.ReadString();
                var expiresDelta = br.ReadInt32();
                if (expiresDelta < 0)
                {
                    return;
                }
                var expires = DateTime.UtcNow.AddSeconds(expiresDelta);
                var cnt = br.ReadInt32();
                if (cnt < 0 || cnt > MaxNumAttrs)
                {
                    return;
                }
                var attrs = new Dictionary<string, string>(cnt);
                for (int i = 0; i < cnt; ++i)
                {
                    var k = br.ReadString();
                    var v = br.ReadString();
                    attrs.Add(k, v);
                }
                callback(msg, cat, con, expires, attrs);
            }
        }

        private static void DecodeServerByeBye(ServerByeByeCallback callback, IPeerDiscoveryMessage msg)
        {
            callback(msg);
        }

        private static void DecodeClientQuery(ClientQueryCallback callback, IPeerDiscoveryMessage msg)
        {
            using (var ms = new MemoryStream(msg.Contents.Array, msg.Contents.Offset + 4, msg.Contents.Count - 4, false))
            using (var br = new BinaryReader(ms))
            {
                var category = br.ReadString();
                callback(msg, category);
            }
        }

        // Sending

        internal void SendServerReply(IPeerDiscoveryMessage msg, string category, Guid uniqueId, string connection, int expirySeconds, IReadOnlyCollection<KeyValuePair<string, string>> attributes)
        {
            Extensions.Reply(transport_, msg, uniqueId, w =>
            {
                w.Write(Proto.ServerReply);
                _SendResourceInfo(w, category, connection, expirySeconds, attributes);
            });
        }

        internal void SendServerHello(string category, Guid uniqueId, string connection, int expirySeconds, IReadOnlyCollection<KeyValuePair<string, string>> attributes)
        {
            Extensions.Broadcast(transport_, uniqueId, w =>
            {
                w.Write(Proto.ServerReply);
                _SendResourceInfo(w, category, connection, expirySeconds, attributes);
            });
        }

        private void _SendResourceInfo(BinaryWriter w, string category, string connection, int expirySeconds, IReadOnlyCollection<KeyValuePair<string, string>> attributes)
        {
            w.Write(category);
            w.Write(connection);
            w.Write(expirySeconds);
            w.Write(attributes.Count);
            foreach (var kvp in attributes)
            {
                w.Write(kvp.Key);
                w.Write(kvp.Value);
            }
        }

        internal void SendServerByeBye(Guid guid)
        {
            Extensions.Broadcast(transport_, guid, w =>
            {
                w.Write(Proto.ServerByeBye);
            });
        }

        internal void SendClientQuery(string category)
        {
            Extensions.Broadcast(transport_, Guid.Empty, (BinaryWriter w) =>
            {
                w.Write(Proto.ClientQuery);
                w.Write(category);
            });
        }

        internal void Dispatch(IPeerDiscoveryMessage msg)
        {
            if (msg.Contents.Count < 4)
            {
                return; // throw
            }
            switch (BitConverter.ToInt32(msg.Contents.Array, msg.Contents.Offset))
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
        /// The list of all local resources of all categories
        private SortedSet<LocalResource> localResources_ = new SortedSet<LocalResource>(new Extensions.ResourceComparer());

        /// Timer for re-announcing resources.
        private Timer timer_;
        /// Time when the timer will fire or MaxValue if the timer is unset.
        private DateTime timerExpiryTime_ = DateTime.MaxValue;

        /// Protocol handler.
        private Proto proto_;

        // Used to prevent any announcements from being sent after the bye-bye messages.
        private bool stopAllAnnouncements_ = false;
        private object announcementsLock_ = new object();

        internal Server(IPeerDiscoveryTransport net)
        {
            proto_ = new Proto(net);
            proto_.OnClientQuery = OnClientQuery;
            timer_ = new Timer(OnServerTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
        }

        void OnClientQuery(IPeerDiscoveryMessage msg, string category)
        {
            LocalResource[] matching;
            lock (this)
            {
                matching = (from lr in localResources_
                            where lr.Category == category
                            select lr).ToArray();
            }
            lock (announcementsLock_)
            {
                if (!stopAllAnnouncements_)
                {
                    foreach (var res in matching)
                    {
                        proto_.SendServerReply(msg, res.Category, res.UniqueId, res.Connection, res.ExpirySeconds, res.Attributes);
                    }
                }
            }
        }

        internal void OnServerTimerExpired(object state)
        {
            var now = DateTime.UtcNow;
            var todo = new List<LocalResource>();
            lock (this)
            {
                foreach (var r in localResources_)
                {
                    if (r.NextAnnounceTime < now)
                    {
                        r.LastAnnouncedTime = now;
                        todo.Add(r);
                    }
                }
                UpdateAnnounceTimer();
            }
            lock (announcementsLock_)
            {
                if (!stopAllAnnouncements_)
                {
                    foreach (var res in todo)
                    {
                        proto_.SendServerHello(res.Category, res.UniqueId, res.Connection, res.ExpirySeconds, res.Attributes);
                    }
                }
            }
        }

        internal void Stop()
        {
            Guid[] data;
            lock (this)
            {
                timer_.Change(Timeout.Infinite, Timeout.Infinite);
                timerExpiryTime_ = DateTime.MaxValue;

                data = localResources_.Select(r => r.UniqueId).ToArray();
            }
            // Wait until the lock is acquired (all announcements in progress have been sent) and stop sending.
            lock (announcementsLock_)
            {
                stopAllAnnouncements_ = true;
            }
            foreach (var guid in data)
            {
                proto_.SendServerByeBye(guid);
            }
            proto_.Stop();
        }

        private void UpdateAnnounceTimer()
        {
            Debug.Assert(Monitor.IsEntered(this)); // Caller should have lock(this)
            var next = localResources_.Min(r => r.NextAnnounceTime);
            if (next != null)
            {
                var now = DateTime.UtcNow;
                var delta = next.Subtract(now);
                timerExpiryTime_ = next;
                timer_.Change((int)Math.Min(Math.Max(delta.TotalMilliseconds + 1, 0), int.MaxValue), -1);
            }
            else // no more resources
            {
                timer_.Change(Timeout.Infinite, Timeout.Infinite);
                timerExpiryTime_ = DateTime.MaxValue;
            }
        }

        internal Task<IDiscoveryResource> PublishAsync(
            string category,
            string connection,
            int expirySeconds,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            var attrs = new Dictionary<string, string>();
            if (attributes != null) // copy so user can't change them behind our back
            {
                foreach (var kvp in attributes)
                {
                    attrs[kvp.Key] = kvp.Value;
                }
            }
            var resource = new LocalResource(category, connection, expirySeconds, attrs);
            resource.Updated = OnResourceUpdated;
            lock (this)
            {
                localResources_.Add(resource); // new local resources get a new guid, always unique
                UpdateAnnounceTimer();
            }

            return Task.FromResult((IDiscoveryResource)resource);
        }

        private void OnResourceUpdated(LocalResource resource)
        {
            resource.LastAnnouncedTime = DateTime.UtcNow;
            lock (announcementsLock_)
            {
                if (!stopAllAnnouncements_)
                {
                    proto_.SendServerHello(resource.Category, resource.UniqueId, resource.Connection, resource.ExpirySeconds, resource.Attributes);
                }
            }
        }

        // Resource which has been created locally. And is owned locally.
        class LocalResource : IDiscoveryResource
        {
            // Each committed edit bumps this serial number.
            // If the serial number of an edit does not match this, then we can detect stale edits.
            private int editSerialNumber_ = 0;
            private volatile Dictionary<string, string> attributes_;

            public LocalResource(string category, string connection, int expirySeconds, Dictionary<string, string> attrs)
            {
                Category = category;
                UniqueId = Guid.NewGuid();
                Connection = connection;
                ExpirySeconds = expirySeconds;
                attributes_ = attrs;
            }

            public Action<LocalResource> Updated;
            public string Category { get; }
            public Guid UniqueId { get; }
            public int ExpirySeconds { get; } // Relative time. Interval from announce to expiration.
            public DateTime LastAnnouncedTime = DateTime.MinValue; // Absolute FileTime.
            public string Connection { get; }
            public IReadOnlyDictionary<string, string> Attributes { get => attributes_; }
            public DateTime NextAnnounceTime
            {
                // Reannounce at 45% of expiry time. On an unreliable network, that gives 2 chances
                // for clients to refresh before expiring.
                get => LastAnnouncedTime.AddSeconds(0.45 * ExpirySeconds);
            }

            class RaceEditException : Exception
            {
                internal RaceEditException() : base("Another edit was made against the same baseline but commited before this one.") { }
            }

            internal Task ApplyEdit(int serial, List<string> removeAttrs, Dictionary<string, string> putAttrs)
            {
                lock (this)
                {
                    if (editSerialNumber_ != serial)
                    {
                        return Task.FromException(new RaceEditException());
                    }
                    editSerialNumber_ += 1;
                    // copy and replace attributes so we don't break existing readers
                    var attrs = new Dictionary<string, string>(attributes_);
                    foreach (var rem in removeAttrs)
                    {
                        attrs.Remove(rem);
                    }
                    foreach (var put in putAttrs)
                    {
                        attrs[put.Key] = put.Value;
                    }
                    attributes_ = attrs;
                    Updated?.Invoke(this);
                    return Task.CompletedTask;
                }
            }

            class Editor : IDiscoveryResourceEditor
            {
                LocalResource resource_;
                int serial_;
                List<string> removeAttrs_ = new List<string>();
                Dictionary<string, string> putAttrs_ = new Dictionary<string, string>();

                internal Editor(LocalResource resource, int serial)
                {
                    resource_ = resource;
                    serial_ = serial;
                }
                public Task CommitAsync() { return resource_.ApplyEdit(serial_, removeAttrs_, putAttrs_); }
                public void PutAttribute(string key, string value) { putAttrs_[key] = value; }
                public void RemoveAttribute(string key) { removeAttrs_.Add(key); }
            }

            public IDiscoveryResourceEditor RequestEdit()
            {
                return new Editor(this, editSerialNumber_);
            }
        }
    }

    class Client
    {
        /// Timer for expiring resources.
        Timer timer_;
        /// Time when the timer will fire or DateTime.MaxValue if the timer is unset.
        DateTime timerExpiryTime_ = DateTime.MaxValue;

        /// The list of all local resources of all categories
        IDictionary<string, CategoryInfo> infoFromCategory_ = new Dictionary<string, CategoryInfo>();

        /// Reverse map resource ID -> category.
        IDictionary<Guid, string> categoryFromResourceId_ = new Dictionary<Guid, string>();

        /// Protocol handler.
        Proto proto_;

        Task updateTask_;
        CancellationTokenSource updateCts_ = new CancellationTokenSource();
        AutoResetEvent updateAvailable_ = new AutoResetEvent(false);

        internal Client(IPeerDiscoveryTransport net)
        {
            proto_ = new Proto(net);
            proto_.OnServerHello = OnServerAnnounce;
            proto_.OnServerByeBye = OnServerByeBye;
            proto_.OnServerReply = OnServerAnnounce;
            timer_ = new Timer(OnClientTimerExpired, null, Timeout.Infinite, Timeout.Infinite);

            updateTask_ = Task.Run(() =>
            {
                var token = updateCts_.Token;
                var handles = new WaitHandle[] { token.WaitHandle, updateAvailable_ };
                var tasksUpdated = new List<DiscoveryTask>();
                while (true)
                {
                    // Wait for either update or cancellation.
                    WaitHandle.WaitAny(handles);
                    token.ThrowIfCancellationRequested();

                    // There has been an update, collect the dirty tasks.
                    lock (this)
                    {
                        foreach (var info in infoFromCategory_.Values)
                        {
                            if (info.IsDirty)
                            {
                                tasksUpdated.AddRange(info.tasks_);
                            }
                        }
                    }

                    // Outside the lock.
                    try
                    {
                        foreach (var t in tasksUpdated)
                        {
                            t.FireUpdated();
                        }
                    }
                    catch(Exception e)
                    {
                        Log.Error(e, "Error while firing update");
                    }
                    tasksUpdated.Clear();
                }
            }, updateCts_.Token);
        }

        internal IDisposedEventDiscoveryTask StartDiscovery(string category)
        {
            lock (this)
            {
                // Create internals for this category if it doesn't already exist.
                CategoryInfo info;
                if (!infoFromCategory_.TryGetValue(category, out info))
                {
                    info = new CategoryInfo(category);
                    infoFromCategory_.Add(category, info);

                    proto_.SendClientQuery(category);
                }
                // start a new task in the category
                var res = new DiscoveryTask(this, info);
                info.tasks_.Add(res);
                return res;
            }
        }

        internal void Stop()
        {
            updateCts_.Cancel();
            try
            {
                updateTask_.Wait();
            }
            catch (AggregateException agg)
            {
                agg.Handle(e => { return e is OperationCanceledException; });
            }
            updateCts_.Dispose();

            lock (this)
            {
                timer_.Change(Timeout.Infinite, Timeout.Infinite);
                timerExpiryTime_ = DateTime.MaxValue;
            }
            proto_.Stop();
        }

        // Resource which we've heard about from a remote
        private class RemoteResource : IDiscoveryResource
        {
            public RemoteResource(string category, Guid uniqueId, string connection, IReadOnlyDictionary<string, string> attrs, DateTime expirationTime)
            {
                Category = category;
                UniqueId = uniqueId;
                Connection = connection;
                Attributes = attrs;
                ExpirationTime = expirationTime;
            }

            public DateTime ExpirationTime;
            public string Category { get; set; }
            public Guid UniqueId { get; }
            public string Connection { get; set; }
            public IReadOnlyDictionary<string, string> Attributes { get; set; }
            public IDiscoveryResourceEditor RequestEdit() { return null; }
        }

        // Internal class which holds the latest results for each category.
        private class CategoryInfo
        {
            internal string category_;

            // Tasks from this category (ephemeral).
            internal IList<DiscoveryTask> tasks_ = new List<DiscoveryTask>();

            // Currently known remote resources. Each time it is updated, we update resourceSerial_ also so that tasks can cache efficiently.
            internal SortedDictionary<Guid, RemoteResource> resourcesRemote_ = new SortedDictionary<Guid, RemoteResource>();

            // This is incremented on each change to the category
            internal int ResourceSerial { get; private set; } = 0;

            internal bool IsDirty { get; private set; } = false;

            internal CategoryInfo(string category)
            {
                category_ = category;
            }

            internal void IncrementSerial()
            {
                ++ResourceSerial;
                IsDirty = true;
            }

            internal void SetClean()
            {
                IsDirty = false;
            }
        }

        internal interface IDisposedEventDiscoveryTask : IDiscoverySubscription
        {
            event Action<IDiscoverySubscription> Disposed;
        }

        // User facing interface for an in-progress discovery operation
        private class DiscoveryTask : IDisposedEventDiscoveryTask
        {
            Client client_;
            CategoryInfo info_;
            IDiscoveryResource[] cachedResources_ = null;
            int cachedResourcesSerial_ = -1;

            public IEnumerable<IDiscoveryResource> Resources
            {
                get
                {
                    var updated = client_.TaskFetchResources(info_, cachedResourcesSerial_);
                    if (updated != null)
                    {
                        cachedResourcesSerial_ = updated.Item1;
                        cachedResources_ = updated.Item2;
                    }
                    return cachedResources_;
                }
            }

            public event Action<IDiscoverySubscription> Updated;
            public event Action<IDiscoverySubscription> Disposed;

            public void FireUpdated()
            {
                Updated?.Invoke(this);
            }

            public void Dispose()
            {
                client_.TaskDispose(this, info_);
                Disposed?.Invoke(this);
                Disposed = null;
            }

            public DiscoveryTask(Client client, CategoryInfo info)
            {
                client_ = client;
                info_ = info;
            }
        }

        // Task helpers

        // return the new list of resources or null if the serial hasn't changed.
        private Tuple<int, IDiscoveryResource[]> TaskFetchResources(CategoryInfo info, int serial)
        {
            lock (this) // Update the cached copy if it has changed
            {
                if (info.ResourceSerial == serial)
                {
                    return null;
                }
                // need a copy since .Values is a reference
                var resources = info.resourcesRemote_.Values.ToArray<IDiscoveryResource>();
                return new Tuple<int, IDiscoveryResource[]>(info.ResourceSerial, resources);
            }
        }

        private void TaskDispose(DiscoveryTask task, CategoryInfo info)
        {
            lock (this)
            {
                info.tasks_.Remove(task);
            }
        }

        // Note: must be called under lock.
        private void SetExpirationTimer(DateTime expiryTime)
        {
            // Use int since UWP does not implement long ctor.
            int deltaMsInt;
            if (expiryTime == DateTime.MaxValue)
            {
                deltaMsInt = Timeout.Infinite;
            }
            else
            {
                var deltaMs = (long)expiryTime.Subtract(DateTime.UtcNow).TotalMilliseconds;
                // Round up to the next ms to ensure the (finer grained) fileTime has passed.
                // Also ensure we have a positive delta or the timer will not work.
                deltaMs = Math.Max(deltaMs + 1, 0);
                deltaMsInt = (int)Math.Min(deltaMs, int.MaxValue);
            }
            timer_.Change(deltaMsInt, Timeout.Infinite);
            timerExpiryTime_ = expiryTime;
        }

        private void OnClientTimerExpired(object state)
        {
            DateTime nextExpiryFileTime = DateTime.MaxValue;
            bool updated = false;
            lock (this)
            {
                // Search and delete any expired resources.
                // Also check the next expiry so we can reset the timer.
                DateTime nowDate = DateTime.UtcNow;
                foreach (var info in infoFromCategory_.Values)
                {
                    var expired = new List<Guid>();
                    foreach (var kvp in info.resourcesRemote_)
                    {
                        if (kvp.Value.ExpirationTime <= nowDate) //resource expired?
                        {
                            expired.Add(kvp.Key);
                        }
                        else if (kvp.Value.ExpirationTime < nextExpiryFileTime) // resource next to expire?
                        {
                            nextExpiryFileTime = kvp.Value.ExpirationTime;
                        }
                    }
                    if (expired.Any())
                    {
                        foreach (var exp in expired)
                        {
                            info.resourcesRemote_.Remove(exp);
                            categoryFromResourceId_.Remove(exp);
                        }
                        info.IncrementSerial();
                        updated = true;
                    }
                }
                SetExpirationTimer(nextExpiryFileTime);
            }

            if (updated)
            {
                updateAvailable_.Set();
            }
        }

        // The body of ServerHello and ServerReply is identical so we reuse the code.
        private void OnServerAnnounce(IPeerDiscoveryMessage msg, string category, string connection, DateTime expiresTime, Dictionary<string, string> attributes)
        {
            var guid = msg.StreamId;
            lock (this)
            {
                // see if the category is relevant to us, we created an info in StartDiscovery if so.
                CategoryInfo info;
                if (!infoFromCategory_.TryGetValue(category, out info))
                {
                    return; // we don't care about this category
                }
                RemoteResource resource;
                bool updated = false;
                if (!info.resourcesRemote_.TryGetValue(guid, out resource)) // new resource
                {
                    resource = new RemoteResource(category, guid, connection, attributes, expiresTime);
                    info.resourcesRemote_[guid] = resource;
                    categoryFromResourceId_[guid] = category;
                    updated = true;
                }
                else // existing resource, has it changed?
                {
                    if (resource.Category != category)
                    {
                        // todo: We cannot handle this correctly for now, since we index resources by category
                    }
                    if (resource.Connection != connection)
                    {
                        resource.Connection = connection;
                        updated = true;
                    }
                    if (!Extensions.DictionariesEqual(resource.Attributes, attributes))
                    {
                        resource.Attributes = attributes;
                        updated = true;
                    }
                    if (resource.ExpirationTime != expiresTime)
                    {
                        resource.ExpirationTime = expiresTime;
                    }
                }
                // If this expiry is sooner than the current timer, we need to reset the timer.
                if (expiresTime < timerExpiryTime_)
                {
                    SetExpirationTimer(expiresTime);
                }
                if (updated)
                {
                    info.IncrementSerial();
                    updateAvailable_.Set();
                }
            }
        }

        private void OnServerByeBye(IPeerDiscoveryMessage msg)
        {
            var guid = msg.StreamId;
            lock (this)
            {
                if (categoryFromResourceId_.TryGetValue(guid, out string category))
                {
                    categoryFromResourceId_.Remove(guid);
                    var info = infoFromCategory_[category];
                    info.resourcesRemote_.Remove(guid);
                    info.IncrementSerial();
                    updateAvailable_.Set();
                }
            }
        }
    }

    /// <summary>
    /// Simple discovery agent with pluggable network transport.
    /// </summary>
    public class PeerDiscoveryAgent : IDiscoveryAgent
    {
        /// The transport for this agent.
        private readonly IPeerDiscoveryTransport transport_;
        private readonly object transportStartStopLock_ = new object();

        private Server server_;
        private Client client_;
        private Options options_;
        private bool isDisposed_ = false;

        // Counts how many things (local resources or discovery tasks) are using the transport.
        private int transportRefCount_ = 0;

        /// Options for this agent
        public class Options
        {
            public int ResourceExpirySec = 30;
        }

        /// Create a discovery agent with the given transport and options.
        public PeerDiscoveryAgent(IPeerDiscoveryTransport transport, Options options = null)
        {
            transport_ = transport;
            options_ = options ?? new Options();
        }

        // public interface implementations

        public IDiscoverySubscription Subscribe(string category)
        {
            if (isDisposed_)
            {
                throw new ObjectDisposedException("PeerDiscoveryAgent");
            }
            if (client_ == null)
            {
                client_ = new Client(transport_);
            }
            AddRefToTransport();
            var task = client_.StartDiscovery(category);
            task.Disposed += RemoveRefFromTransport;
            return task;
        }

        public Task<IDiscoveryResource> PublishAsync(
            string category,
            string connection,
            IReadOnlyDictionary<string, string> attributes = null,
            CancellationToken token = default)
        {
            if (isDisposed_)
            {
                throw new ObjectDisposedException("PeerDiscoveryAgent");
            }
            if (server_ == null)
            {
                server_ = new Server(transport_);
            }
            AddRefToTransport();
            return server_.PublishAsync(category, connection, options_.ResourceExpirySec, attributes, token);
        }

        private void AddRefToTransport()
        {
            lock (transportStartStopLock_)
            {
                if (transportRefCount_ == 0)
                {
                    transport_.Start();
                }
                ++transportRefCount_;
            }
        }

        private void RemoveRefFromTransport(IDiscoverySubscription _)
        {
            lock (transportStartStopLock_)
            {
                --transportRefCount_;
                if (transportRefCount_ == 0)
                {
                    transport_.Stop();
                }
            }
        }

        public void Dispose()
        {
            if (isDisposed_)
            {
                return;
            }
            isDisposed_ = true;

            server_?.Stop();
            client_?.Stop();

            // Give some time for the ByeBye message to be sent before shutting down the sockets.
            // todo is there a smarter way to do this?
            Task.Delay(1).Wait();

            // Stop the network.
            lock(transportStartStopLock_)
            {
                if (transportRefCount_ > 0)
                {
                    transport_.Stop();
                }

                // Prevent later disposals from trying to stop the transport again (will see negative refcount).
                // Since the object is disposed refcount cannot return to be positive.
                transportRefCount_ = 0;
            }
        }
    }
}
