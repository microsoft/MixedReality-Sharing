using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface ISerializer
    {
        int PriorityOrder { get; }

        bool SupportsType(Type type);

        void Serialize<T>(T data, Span<byte> output) where T : struct;

        void TryDeserialize<T>(ReadOnlySpan<byte> data, bool outType) where T : struct;
    }
}
