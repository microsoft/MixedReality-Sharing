// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// This is the unsubscribe token for a subscription, dispose of it to unsubscribe.
    /// </summary>
    public class SubscriptionToken : DisposablePointerBase
    {
        internal SubscriptionToken(IntPtr subscriptionPointer)
            : base(subscriptionPointer)
        {
        }

        protected override void ReleasePointer(IntPtr pointer)
        {
            StateSyncAPI.Subscription_Release(pointer);
        }
    }
}
