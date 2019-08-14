// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    public interface ISessionFactory<TConfiguration> where TConfiguration : IRoomConfiguration
    {
        Task<ISession> JoinSessionAsync(TConfiguration configuration, IReadOnlyDictionary<string, string> attributes, CancellationToken cancellationToken);

        Task<KeyValuePair<TConfiguration, ISession>> HostNewRoomAsync(IDictionary<string, string> attributes, CancellationToken cancellationToken);
    }
}
