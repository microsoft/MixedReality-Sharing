// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// Helper class acting as a key for opened channels.
    /// </summary>
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
