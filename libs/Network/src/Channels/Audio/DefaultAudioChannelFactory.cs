// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;
using Microsoft.MixedReality.Sharing.Utilities.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// The default factory for the <see cref="AudioChannel"/> based on BasicDataChannel.
    /// </summary>
    public class DefaultAudioChannelFactory : IChannelFactory<AudioChannel>
    {
        private class DefaultAudioChannel : AudioChannel
        {
            private StreamPipe streamingPipe = null;
            private StreamPipe listeningPipe = null;

            private readonly BasicDataChannel basicChannel;

            public DefaultAudioChannel(string id, BasicDataChannel basicChannel)
                : base(id)
            {
                this.basicChannel = basicChannel;
                this.basicChannel.MessageReceived += OnDataMessageReceived;
            }

            protected override Stream OnBeginListening()
            {
                listeningPipe = new StreamPipe();
                return listeningPipe.Output;
            }

            protected override Stream OnBeginStreaming()
            {
                streamingPipe = new StreamPipe();
                Task.Run(() => StreamDataAsync(streamingPipe.Output, DisposeCancellationToken), DisposeCancellationToken).FireAndForget();
                return streamingPipe.Input;
            }

            protected async Task StreamDataAsync(Stream streamToReadFrom, CancellationToken cancellationToken)
            {
                byte[] buffer = new byte[basicChannel.PacketSize];
                while (streamToReadFrom.CanRead && !cancellationToken.IsCancellationRequested)
                {
                    int numRead = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    // This is done silly
                    if (numRead > 0 && numRead < buffer.Length)
                    {
                        basicChannel.SendMessage(new MemoryStream(buffer, 0, numRead));
                    }
                    else if (numRead > 0)
                    {
                        basicChannel.SendMessage(buffer);
                    }
                }
            }

            private void OnDataMessageReceived(IEndpoint endpoint, ReadOnlySpan<byte> message)
            {
                if (listeningPipe != null)
                {
                    // This is inefficient, it's a copy
                    listeningPipe.Input.Write(message.ToArray(), 0, message.Length);
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the factory providing it the factory to be used for the basic data channel transfer.
        /// </summary>
        /// <param name="basicChannelFactory">The factory to use for basic data channel.</param>
        public DefaultAudioChannelFactory()
        {
        }

        /// <summary>
        /// Opens a new <see cref="AudioChannel"/> for the specified session.
        /// </summary>
        /// <param name="session">The sesson for which the channel should be opened.</param>
        /// <param name="channelId">The id of the channel to open.</param>
        /// <returns>The opened <see cref="AudioChannel"/>.</returns>
        public AudioChannel GetChannel(ISession session, string channelId)
        {
            string channelIdToUse = channelId == null ? typeof(DefaultAudioChannelFactory).FullName : $"{channelId}.DataChannel";
            BasicDataChannel channel = session.GetChannel<BasicDataChannel>(channelIdToUse);

            return new DefaultAudioChannel(channelId, channel);
        }

        /// <summary>
        /// Opens a new <see cref="AudioChannel"/> for the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint for which the channel should be opened.</param>
        /// <param name="channelId">The id of the channel to open.</param>
        /// <returns>The opened <see cref="AudioChannel"/>.</returns>
        public AudioChannel GetChannel(IEndpoint endpoint, string channelId)
        {
            string channelIdToUse = channelId == null ? typeof(DefaultAudioChannelFactory).FullName : $"{channelId}.DataChannel";
            BasicDataChannel channel = endpoint.GetChannel<BasicDataChannel>(channelIdToUse);

            return new DefaultAudioChannel(channelId, channel);
        }
    }
}
