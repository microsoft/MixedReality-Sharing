using System;

namespace Microsoft.MixedReality.Sharing
{
    internal struct ChannelMapKey : IEquatable<ChannelMapKey>
    {
        public Type Type { get; }

        public string ChannelId { get; }

        public ChannelMapKey(Type type, string channelId)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            ChannelId = channelId;
        }

        public override bool Equals(object obj)
        {
            return obj is ChannelMapKey && Equals((ChannelMapKey)obj);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode() ^ (ChannelId?.GetHashCode() ?? 0);
        }

        public bool Equals(ChannelMapKey other)
        {
            return Equals(Type, other.Type)
                && Equals(ChannelId, other.ChannelId);
        }
    }
}
