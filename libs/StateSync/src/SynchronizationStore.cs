// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public delegate void KeysChangedHandler(LightweightKeySet updatedKeys, LightweightSnapshot snapshot);
    public delegate void SnapshotHandler(LightweightSnapshot snapshot);

    public class SynchronizationStore
    {
        public event KeysChangedHandler KeysChanged;

        private readonly ConcurrentDictionary<Type, ISerializer> resolvedSerializers = new ConcurrentDictionary<Type, ISerializer>();
        private readonly SortedSet<ISerializer> serializers;

        public SynchronizationStore(IEnumerable<ISerializer> serializers)
        {
            this.serializers = new SortedSet<ISerializer>(serializers, Comparer<ISerializer>.Create((x, y) => x.PriorityOrder.CompareTo(y.PriorityOrder)));
        }

        public void UsingSnapshot(SnapshotHandler handler)
        {
            
        }

        public void ReleaseSnapshot(LightweightSnapshot snapshot)
        {

        }

        public SynchronizationKey CreateKey(string value)
        {
            return new SynchronizationKey(this, Encoding.ASCII.GetBytes(value), value.GetHashCode());
        }

        internal void ReleaseKey(SynchronizationKey key)
        {

        }

        internal T Get<T>(LightweightSnapshot snapshot, ReadOnlySpan<byte> keyValue, int hashCode) { return default(T); }

        internal T Get<T>(LightweightSnapshot snapshot, string key) { return default(T); }

        internal ISerializer GetSerializer(Type type)
        {
            return resolvedSerializers.GetOrAdd(type, t => serializers.FirstOrDefault(s => s.SupportsType(t)));
        }
    }
}
