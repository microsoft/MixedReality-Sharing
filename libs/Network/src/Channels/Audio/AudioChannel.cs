// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Channels
{
    /// <summary>
    /// A simple audio channel.
    /// </summary>
    public abstract class AudioChannel : ChannelBase
    {
        private Stream listeningStream;
        private CancellationTokenSource streamingCts;

        /// <summary>
        /// Gets whether there is audio being streamed on this channel.
        /// </summary>
        public bool IsStreaming
        {
            get
            {
                lock (LockObject)
                {
                    return streamingCts != null && !streamingCts.Token.IsCancellationRequested;
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
        public void BeginStreamingAudio(Stream streamToReadFrom)
        {
            lock (LockObject)
            {
                ThrowIfDisposed();

                if (streamingCts != null)
                {
                    throw new InvalidOperationException("Single streaming operation allowed on audio channel.");
                }

                streamingCts = CancellationTokenSource.CreateLinkedTokenSource(DisposeCancellationToken);
                BeginStreamingAsync(streamToReadFrom, streamingCts.Token).FireAndForget();
            }
        }

        public Stream BeginListening()
        {
            lock (LockObject)
            {
                ThrowIfDisposed();

                if (listeningStream != null)
                {
                    throw new InvalidOperationException("This audio channel already has a listener.");
                }

                return listeningStream = CreateListeningStream();
            }
        }

        private async Task BeginStreamingAsync(Stream streamToReadFrom, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() => OnStreamDataAsync(streamToReadFrom, cancellationToken), cancellationToken);
            }
            finally
            {
                lock (LockObject)
                {
                    // Clean up in case we finished due error or stream closing.
                    streamingCts?.Cancel();
                    streamingCts?.Dispose();
                    streamingCts = null;
                }
            }
        }

        /// <summary>
        /// Stop streaming audio data.
        /// </summary>
        public void StopStreamingAudio()
        {
            lock (LockObject)
            {
                ThrowIfDisposed();

                if (streamingCts == null)
                {
                    throw new InvalidOperationException("No streaming operation is present on the audio channel.");
                }

                streamingCts.Cancel();
                streamingCts.Dispose();
                streamingCts = null;
            }
        }

        protected override void OnManagedDispose()
        {
            listeningStream?.Dispose();
            listeningStream = null;
        }

        /// <summary>
        /// Implemment this in the inheriting class to stream the audio data.
        /// </summary>
        /// <param name="streamToReadFrom">The stream to read from.</param>
        /// <param name="cancellationToken">The cancellation token to interrupt streaming.</param>
        /// <returns></returns>
        protected abstract Task OnStreamDataAsync(Stream streamToReadFrom, CancellationToken cancellationToken);

        /// <summary>
        /// Implement this in the inheritng class to receive streamed audio.
        /// </summary>
        /// <returns>The stream that can be read for audio.</returns>
        protected abstract Stream CreateListeningStream();
    }
}
