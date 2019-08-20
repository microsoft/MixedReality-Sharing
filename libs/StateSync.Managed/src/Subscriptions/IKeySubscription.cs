// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Implement this interface to receive value updates for a key.
    /// </summary>
    public interface IKeySubscription
    {
        /// <summary>
        /// This method is invoked when a subscribed key's value changed.
        /// </summary>
        /// <param name="key">The key for which the update occured.</param>
        /// <param name="previousSnapshot">The snapshot prior to the update.</param>
        /// <param name="currentSnapshot">The current snapshot (post the update).</param>
        /// <param name="subkeysAdded">A set of subkeys that were added as part of this update.</param>
        /// <param name="subkeysUpdated">A set of subkeys for which values were updated as part of this update.</param>
        /// <param name="subkeysRemoved">A set of subkeys that were removed as part of this update.</param>
        void KeyDataUpdated(KeyRef key, Snapshot previousSnapshot, Snapshot currentSnapshot, ReadOnlySpan<ulong> subkeysAdded, ReadOnlySpan<ulong> subkeysUpdated, ReadOnlySpan<ulong> subkeysRemoved);
    }
}
