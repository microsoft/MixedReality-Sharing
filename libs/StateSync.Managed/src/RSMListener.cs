using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface RSMListener
    {
        void OnEntryComitted(ulong sequentialEntryId,
            Guid commandId,
            ReadOnlySpan<byte> entry);

        void OnLogFastForward(ReadOnlySpan<byte> entry);
    }
}
