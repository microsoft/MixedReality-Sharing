// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync.Snapshots
{
    //WIP code
    public ref struct UpdatedKey
    {
        public InternedBlobRef Key { get { throw new NotImplementedException(); } }

        public ReadOnlySpan<ulong> InsertedSubkeys { get { throw new NotImplementedException(); } }
        public ReadOnlySpan<ulong> UpdatedSubkeys { get { throw new NotImplementedException(); } }
        public ReadOnlySpan<ulong> RemovedSubkeys { get { throw new NotImplementedException(); } }
    }
}
