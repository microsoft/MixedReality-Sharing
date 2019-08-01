using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public class SynchronizationKeyComparer : IEqualityComparer<SynchronizationKey>, IEqualityComparer, IComparer<SynchronizationKey>, IComparer
    {
        public static SynchronizationKeyComparer Instance { get; } = new SynchronizationKeyComparer();

        private SynchronizationKeyComparer() { }

        public int Compare(SynchronizationKey x, SynchronizationKey y)
        {
            return x.KeyValue.Span.SequenceCompareTo(y.KeyValue.Span);
        }

        public int Compare(SynchronizationKey x, string y)
        {
            return Compare(x.KeyValue, y);
        }

        internal int Compare(ReadOnlySpan<byte> span, string str)
        {
            int stringLength = str?.Length ?? throw new ArgumentNullException(nameof(str));
            int minLength = Math.Min(span.Length, stringLength);

            for (int i = 0; i < minLength; i++)
            {
                byte t = span[i];
                char o = str[i];

                if (t > o)
                {
                    return 1;
                }
                else if (t < o)
                {
                    return -1;
                }
            }

            if (span.Length > stringLength)
            {
                return 1;
            }
            else if (span.Length < stringLength)
            {
                return -1;
            }

            return 0;
        }

        public int Compare(SynchronizationKey x, object y)
        {
            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            if (y is SynchronizationKey)
            {
                return Compare(x, (SynchronizationKey)y);
            }
            else if (y is string)
            {
                return Compare(x, (string)y);
            }

            throw new ArgumentException($"{nameof(SynchronizationKeyComparer)}.{nameof(Compare)} is not supported for type '{y.GetType().FullName}'", nameof(y));
        }

        public int Compare(object x, object y)
        {
            if (x is SynchronizationKey)
            {
                return Compare((SynchronizationKey)x, y);
            }
            else if (y is SynchronizationKey)
            {
                return Compare((SynchronizationKey)y, x);
            }
            else
            {
                throw new ArgumentException($"{nameof(SynchronizationKeyComparer)}.{nameof(Compare)} is not supported for types '{x.GetType().FullName}' and '{y.GetType().FullName}'");
            }
        }

        public bool Equals(SynchronizationKey x, SynchronizationKey y)
        {
            return x.KeyValue.Span.SequenceEqual(y.KeyValue.Span);
        }

        public bool Equals(SynchronizationKey x, string y)
        {
            return Compare(x, y) == 0;
        }

        public bool Equals(SynchronizationKey x, object y)
        {
            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            if (y is SynchronizationKey)
            {
                return Equals(x, (SynchronizationKey)y);
            }
            else if (y is string)
            {
                return Equals(x, (string)y);
            }

            throw new ArgumentException($"{nameof(SynchronizationKeyComparer)}.{nameof(Equals)} is not supported for type '{y.GetType().FullName}'", nameof(y));
        }

        public new bool Equals(object x, object y)
        {
            if (x is SynchronizationKey)
            {
                return Equals((SynchronizationKey)x, y);
            }
            else if (y is SynchronizationKey)
            {
                return Equals((SynchronizationKey)y, x);
            }
            else
            {
                throw new ArgumentException($"{nameof(SynchronizationKeyComparer)}.{nameof(Equals)} is not supported for types '{x.GetType().FullName}' and '{y.GetType().FullName}'");
            }
        }

        public int GetHashCode(SynchronizationKey obj)
        {
            return obj.GetHashCode();
        }

        public int GetHashCode(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (obj is SynchronizationKey)
            {
                return GetHashCode((SynchronizationKey)obj);
            }

            throw new ArgumentException($"{nameof(SynchronizationKeyComparer)}.{nameof(GetHashCode)} is not supported for type '{obj.GetType().FullName}'");
        }
    }
}
