// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface IEndpoint
    {
        /// <summary>
        /// Identifies the endpoint.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Opens a channel to communicate to the other participant.
        /// </summary>
        IChannel CreateChannel(IChannelCategory category);
    }

    public interface IEndpointFactory
    {
        /// <summary>
        /// Inflates a <see cref="IEndpoint"/> from its ID.
        /// </summary>
        IEndpoint GetEndpoint(string id);
    }
}
