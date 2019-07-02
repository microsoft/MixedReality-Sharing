// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Network
{
    public class ChannelConfig
    {
        string Name { get; }
        // TODO
    }

    public interface IChannel : IDisposable
    {
        ChannelConfig Config { get; }

        IEndpoint Endpoint { get; }

        void SendMessage(byte[] message);

        event EventHandler<byte[]> MessageReceived;
    }
}
