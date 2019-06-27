// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Network
{
    public enum ChannelState
    {
        Connected,
        ConnectionLost,
        Disconnected
    }

    public interface IChannel
    {
        event Action<ChannelState> StateChanged;

        ChannelState State { get; }

        Stream Stream { get; }
    }
}
