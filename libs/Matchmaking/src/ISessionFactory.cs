// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface ISessionFactory<TConfiguration> where TConfiguration : IRoomConfiguration
    {
        Task<ISession> JoinSessionAsync(TConfiguration configuration);

        Task<TConfiguration> HostNewRoomAsync(CancellationToken cancellationToken);
    }
}
