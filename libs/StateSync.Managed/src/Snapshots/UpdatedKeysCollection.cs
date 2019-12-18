// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync.Snapshots
{
    //WIP code
    public ref struct UpdatedKeysCollection
    {
        public readonly ulong Count;
        public bool IsEmpty { get { return Count == 0; } }

        public UpdatedKeysEnumerator GetEnumerator() { throw new NotImplementedException(); }
    }
}
