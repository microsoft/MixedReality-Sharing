// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface IMessage
    {
        Task<Stream> OpenReadAsync(CancellationToken cancellationToken);
    }
}
