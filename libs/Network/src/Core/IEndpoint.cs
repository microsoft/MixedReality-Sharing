// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing
{
    public interface IEndpoint
    {
        ISession Session { get; }

        /// <summary>
        /// Opens a channel to communicate to the other participant.
        /// </summary>
        Task<TChannel> GetChannelAsync<TChannel>(string channelId, CancellationToken cancellationToken) where TChannel : IChannel;
    }
}
