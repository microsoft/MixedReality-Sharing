// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface NetworkManager
    {
        NetworkConnection GetConnection(InternedBlobRef connectionString);

        bool PollMessage(NetworkListener listener);
    }
}
