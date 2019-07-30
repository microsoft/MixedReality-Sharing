// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using System;

namespace Microsoft.MixedReality.Sharing
{
    public enum ChannelStatus
    {
        Connected,
        Disconnected,
        Disposed
    }

    public interface IChannel : IDisposable
    {
        string Id { get; }

        ChannelStatus Status { get; }

        ///// <summary>
        ///// Try to re-establish the channel. If <see cref="IsOk"/> returns false, this might restore the channel
        ///// status and make it available for sending/receiving again.
        ///// </summary>
        //Task<bool> TryReconnectAsync(CancellationToken cancellationToken);
    }
}
