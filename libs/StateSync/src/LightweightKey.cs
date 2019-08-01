using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public ref struct LightweightKeySet
    {
        public int Count { get; }

        public bool TryGetKey(string searchKey, out LightweightKey key)
        {
            key = default;
            return false;
        }

        public bool ContainsKey(SynchronizationKey key)
        {
            return false;
        }
    }

    public ref struct LightweightKey
    {
        internal ReadOnlySpan<byte> KeyValue { get; }
        internal int HashCode { get; }

        public LightweightKey(ReadOnlySpan<byte> keyData, int hashCode)
        {
            KeyValue = keyData;
            HashCode = hashCode;
        }

        public SynchronizationKey AsSynchronizationKey()
        {
            // TODO
            return default; //  return new SynchronizationKey(keyData, hashCode);
        }

        public override bool Equals(object obj)
        {
            throw new NotSupportedException("Convert this to a SynchronizationKey first by calling AsSynchronizationKey().");
        }

        public override string ToString()
        {
            throw new NotSupportedException("Convert this to a SynchronizationKey first by calling AsSynchronizationKey().");
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException("Convert this to a SynchronizationKey first by calling AsSynchronizationKey().");
        }
    }
}
