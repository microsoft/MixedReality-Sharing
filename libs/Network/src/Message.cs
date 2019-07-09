﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Simple <see cref="IMessage"/> implementation.
    /// </summary>
    public class Message : IMessage
    {
        public IEndpoint Sender { get; }

        public IChannelCategory Category { get; }

        public byte[] Payload { get; }

        public Message(IEndpoint sender, IChannelCategory category, byte[] payload)
        {
            Sender = sender;
            Category = category;
            Payload = payload;
        }
    }
}
