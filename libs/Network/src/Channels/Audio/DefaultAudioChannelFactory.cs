// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// The default factory for the <see cref="AudioChannel"/> based on BasicDataChannel.
    /// </summary>
    public class DefaultAudioChannelFactory : IChannelFactory<AudioChannel>
    {
        private class ListeningStream : Stream
        {
            private int bytesRead = 0;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => bytesRead;

            public override long Position
            {
                get => bytesRead;
                set { }
            }

            public override void Flush() { }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return base.ReadAsync(buffer, offset, count, cancellationToken);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
        private class DefaultAudioChannel : AudioChannel
        {
            private readonly BasicDataChannel basicChannel;

            public DefaultAudioChannel(string id, BasicDataChannel basicChannel)
                : base(id)
            {
                this.basicChannel = basicChannel;
            }

            protected override async Task OnStreamDataAsync(Stream streamToReadFrom, CancellationToken cancellationToken)
            {
                byte[] buffer = new byte[basicChannel.PacketSize];
                while (streamToReadFrom.CanRead && !cancellationToken.IsCancellationRequested)
                {
                    int numRead = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    // This is done silly
                    if (numRead > 0 && numRead < buffer.Length)
                    {
                        await basicChannel.SendMessageAsync(new MemoryStream(buffer, 0, numRead), cancellationToken);
                    }
                    else if (numRead > 0)
                    {
                        await basicChannel.SendMessageAsync(buffer, cancellationToken);
                    }
                }
            }

            protected override Stream CreateListeningStream()
            {
                return null;
            }
        }

        private readonly IChannelFactory<BasicDataChannel> basicChannelFactory;

        /// <summary>
        /// Gets the name of this channel factory.
        /// </summary>
        public string Name => $"AudioChannel Factory built on {nameof(BasicDataChannel)}.";

        /// <summary>
        /// Creates a new instance of the factory providing it the factory to be used for the basic data channel transfer.
        /// </summary>
        /// <param name="basicChannelFactory">The factory to use for basic data channel.</param>
        public DefaultAudioChannelFactory(IChannelFactory<BasicDataChannel> basicChannelFactory)
        {
            this.basicChannelFactory = basicChannelFactory;
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
            BasicDataChannel channel = basicChannelFactory.GetChannel(session, channelIdToUse);

            return new DefaultAudioChannel(channelIdToUse, channel);
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
            BasicDataChannel channel = basicChannelFactory.GetChannel(endpoint, channelIdToUse);

            return new DefaultAudioChannel(channelIdToUse, channel);
        }
    }
}
