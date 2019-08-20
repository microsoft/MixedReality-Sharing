using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    // THIS file is for discussion, it's tricky if we want to enumerate subkey/value pairs with ReadOnlySpan<T> but doable
    // We can optionally just enumerate subkeys
    public struct SubkeyValuePair
    {
        internal delegate ReadOnlySpan<byte> ValueGetter(ulong subkey);

        private readonly ValueGetter valueGetter;

        public ulong Subkey { get; }

        public ReadOnlySpan<byte> Value => valueGetter(Subkey);

        internal SubkeyValuePair(ulong subkey, ValueGetter valueGetter)
        {
            Subkey = subkey;
            this.valueGetter = valueGetter;
        }
    }

    internal class _SubkeyValueCollection : IReadOnlyCollection<SubkeyValuePair>
    {
        private readonly Snapshot snapshot;
        private readonly IntPtr keyPointer;

        public int Count { get; }

        public _SubkeyValueCollection(Snapshot snapshot, IntPtr keyPointer, int count)
        {
            this.snapshot = snapshot;
            this.keyPointer = keyPointer;
            Count = count;
        }

        public IEnumerator<SubkeyValuePair> GetEnumerator()
        {
            return new SubkeyValueEnumerator(snapshot, keyPointer);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal struct SubkeyValueEnumerator : IEnumerator<SubkeyValuePair>
    {
        private readonly Snapshot snapshot;
        private readonly IntPtr keyPointer;
        private ulong currentKey;
        private IntPtr currentValuePointer;

        public SubkeyValuePair Current
        {
            get
            {
                if (currentValuePointer == IntPtr.Zero)
                {
                    throw new InvalidOperationException("The enumerator has not been started, call MoveNext");
                }

                return new SubkeyValuePair(currentKey, GetValue);
            }
        }

        object IEnumerator.Current => Current;

        internal SubkeyValueEnumerator(Snapshot snapshot, IntPtr keyPointer)
        {
            this.snapshot = snapshot;
            this.keyPointer = keyPointer;
            currentKey = 0;
            currentValuePointer = IntPtr.Zero;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        private ReadOnlySpan<byte> GetValue(ulong key)
        {
            if (currentKey == key)
            {
                return new ReadOnlySpan<byte>();
            }
            else
            {
                if (!snapshot.TryGetValue(new KeyRef(keyPointer), key, out ReadOnlySpan<byte> toReturn))
                {
                    throw new InvalidOperationException();
                }

                return toReturn;
            }
        }
    }
}
