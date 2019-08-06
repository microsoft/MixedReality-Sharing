// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// A simple audio channel.
    /// </summary>
    public abstract class AudioChannel : ChannelBase
    {
        private Stream sendingStream;
        private Stream listeningStream;

        /// <summary>
        /// Gets whether there is audio being streamed on this channel.
        /// </summary>
        public bool IsStreaming
        {
            get
            {
                lock (DisposeLockObject)
                {
                    return sendingStream != null && sendingStream.CanWrite;
                }
            }
        }

        /// <summary>
        /// Instantiates a new <see cref="AudioChannel"/>
        /// </summary>
        /// <param name="id">The id of this channel.</param>
        protected AudioChannel(string id) : base(id) { }

        /// <summary>
        /// Begin streaming audio data on this channel from the given stream.
        /// </summary>
        /// <param name="streamToReadFrom">Audio data to stream.</param>
        public Stream BeginStreaming()
        {
            lock (DisposeLockObject)
            {
                ThrowIfDisposed();

                if (sendingStream != null && sendingStream.CanWrite)
                {
                    throw new InvalidOperationException("Single streaming operation allowed on audio channel.");
                }

                sendingStream?.Dispose();
                return sendingStream = OnBeginStreaming();
            }
        }

        public Stream BeginListening()
        {
            lock (DisposeLockObject)
            {
                ThrowIfDisposed();

                if (listeningStream != null && listeningStream.CanRead)
                {
                    throw new InvalidOperationException("This audio channel already has a listener.");
                }

                listeningStream?.Dispose();
                return listeningStream = OnBeginListening();
            }
        }

        protected abstract Stream OnBeginStreaming();

        protected abstract Stream OnBeginListening();

        protected override void OnManagedDispose()
        {
            sendingStream?.Dispose();
            sendingStream = null;

            listeningStream?.Dispose();
            listeningStream = null;
        }
    }
}
