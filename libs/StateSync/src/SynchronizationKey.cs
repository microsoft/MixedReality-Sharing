using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public class SynchronizationKey : DisposableBase, IComparable<SynchronizationKey>, IEquatable<SynchronizationKey>, IComparable<string>, IEquatable<string>, IEnumerable<char>, IEnumerable, IComparable, IConvertible
    {
        private readonly SynchronizationStore synchronizationStore;
        private readonly ReadOnlyMemory<byte> keyValue;
        private readonly int hashCode;

        private string stringValue;

        internal ReadOnlyMemory<byte> KeyValue
        {
            get
            {
                ThrowIfDisposed();

                return keyValue;
            }
        }

        internal int HashCode
        {
            get
            {
                ThrowIfDisposed();

                return hashCode;
            }
        }

        internal SynchronizationKey(SynchronizationStore synchronizationStore, ReadOnlyMemory<byte> keyValue, int hashCode, string stringValue = null)
        {
            this.synchronizationStore = synchronizationStore;
            this.keyValue = keyValue;
            this.hashCode = hashCode;
            this.stringValue = stringValue;
        }

        protected override void OnManagedDispose()
        {
            base.OnManagedDispose();

            synchronizationStore.ReleaseKey(this);
        }

        int IComparable<SynchronizationKey>.CompareTo(SynchronizationKey other)
        {
            ThrowIfDisposed();

            return SynchronizationKeyComparer.Instance.Compare(this, other);
        }

        int IComparable<string>.CompareTo(string other)
        {
            ThrowIfDisposed();

            return SynchronizationKeyComparer.Instance.Compare(this, other);
        }

        int IComparable.CompareTo(object obj)
        {
            ThrowIfDisposed();

            return SynchronizationKeyComparer.Instance.Compare(this, obj);
        }

        bool IEquatable<SynchronizationKey>.Equals(SynchronizationKey other)
        {
            ThrowIfDisposed();

            return SynchronizationKeyComparer.Instance.Equals(this, other);
        }

        bool IEquatable<string>.Equals(string other)
        {
            ThrowIfDisposed();

            return SynchronizationKeyComparer.Instance.Equals(this, other);
        }

        IEnumerator<char> IEnumerable<char>.GetEnumerator()
        {
            ThrowIfDisposed();
            throw new NotSupportedException();
            //ReadOnlySpan<byte> val = keyValue.Span;
            //
            //for (int i = 0; i < keyValue.Length; i++)
            //{
            //    yield return (char)val[i];
            //}
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            ThrowIfDisposed();

            return ((IEnumerable<char>)this).GetEnumerator();
        }

        TypeCode IConvertible.GetTypeCode()
        {
            ThrowIfDisposed();

            return TypeCode.String;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToBoolean(provider);
        }

        byte IConvertible.ToByte(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToByte(provider);
        }

        char IConvertible.ToChar(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToChar(provider);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToDateTime(provider);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToDecimal(provider);
        }

        double IConvertible.ToDouble(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToDouble(provider);
        }

        short IConvertible.ToInt16(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToInt16(provider);
        }

        int IConvertible.ToInt32(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToInt32(provider);
        }

        long IConvertible.ToInt64(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToInt64(provider);
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToSByte(provider);
        }

        float IConvertible.ToSingle(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToSingle(provider);
        }

        string IConvertible.ToString(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ToString();
        }

        object IConvertible.ToType(Type targetType, IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToType(targetType, provider);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToUInt16(provider);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToUInt32(provider);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider)
        {
            ThrowIfDisposed();

            return ((IConvertible)ToString()).ToUInt64(provider);
        }

        public unsafe override string ToString()
        {
            ThrowIfDisposed();

            if (stringValue == null)
            {
                using (MemoryHandle handle = keyValue.Pin())
                {
                    stringValue = Encoding.ASCII.GetString((byte*)handle.Pointer, keyValue.Length);
                }
            }

            return stringValue;
        }

        public override int GetHashCode()
        {
            ThrowIfDisposed();

            return hashCode;
        }
    }
}
