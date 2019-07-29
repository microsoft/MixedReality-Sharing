// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface IEndpoint<TSessionType>
        where TSessionType : class, ISession<TSessionType>
    {
        TSessionType Session { get; }

        /// <summary>
        /// Opens a channel to communicate to the other participant.
        /// </summary>
        Task<IChannel<TSessionType, TMessageType>> GetChannelAsync<TMessageType>(CancellationToken cancellationToken) where TMessageType : IMessage;
    }
}
