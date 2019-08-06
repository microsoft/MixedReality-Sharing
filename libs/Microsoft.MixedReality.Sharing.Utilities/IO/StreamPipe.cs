// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Utilities.IO
{
    public class StreamPipe : DisposableBase
    {
        private struct Message
        {
            public IMemoryOwner<byte> MemoryOwner { get; }
            public int Length { get; }

            public Message(IMemoryOwner<byte> memoryOwner, int length)
            {
                MemoryOwner = memoryOwner;
                Length = length;
            }
        }

        private readonly bool readPartialBuffers;
        private readonly Queue<Message> messagesQueue = new Queue<Message>();

        private int bytesConsumedFromHeadItemOfQueue = 0;
        private TaskCompletionSource<object> messageAvailableTCS = null;

        public Stream Input { get; }

        public Stream Output { get; }

        public StreamPipe(bool readPartialBuffers = true)
        {
            this.readPartialBuffers = readPartialBuffers;

            Input = new InputStream(this);
            Output = new OutputStream(this);
        }

        protected override void OnManagedDispose()
        {
            Input.Dispose();
            Output.Dispose();
        }

        private void AddMessage(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return;
            }

            IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(count);

            buffer.AsSpan()
                .Slice(offset, count)
                .CopyTo(owner.Memory.Span);

            lock (DisposeLockObject)
            {
                messagesQueue.Enqueue(new Message(owner, count));
                messageAvailableTCS?.TrySetResult(null);
                messageAvailableTCS = null;
            }
        }

        private async Task<int> ReadMessageAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int numRead = 0;
            while ((count - numRead) > 0)
            {
                Task toAwait = null;
                lock (DisposeLockObject)
                {
                    if (messagesQueue.Count > 0)
                    {
                        Message message = messagesQueue.Peek();

                        int bytesCanRead = Math.Min(message.Length - bytesConsumedFromHeadItemOfQueue, count - numRead);

                        Copy(message.MemoryOwner.Memory, buffer, offset + numRead, bytesCanRead);

                        bytesConsumedFromHeadItemOfQueue += bytesCanRead;
                        numRead += bytesCanRead;

                        if (message.Length == bytesConsumedFromHeadItemOfQueue)
                        {
                            messagesQueue.Dequeue();
                            message.MemoryOwner.Dispose();
                            bytesConsumedFromHeadItemOfQueue = 0;
                        }
                    }
                    else if (readPartialBuffers && numRead > 0) // Don't wait if we already read something
                    {
                        return numRead;
                    }
                    else
                    {
                        if (messageAvailableTCS == null)
                        {
                            messageAvailableTCS = new TaskCompletionSource<object>();
                        }

                        Task task = messageAvailableTCS.Task;
                        toAwait = Task.Run(() => task);
                    }
                }

                if (toAwait != null)
                {
                    await toAwait.ConfigureAwait(false);
                }
            }

            return numRead;
        }

        private void Copy(Memory<byte> memory, byte[] buffer, int offset, int count)
        {
            Span<byte> destination = buffer.AsSpan().Slice(offset, count);
            memory.Span.Slice(0, count).CopyTo(destination);
        }

        private class OutputStream : OutputStreamBase
        {
            private readonly StreamPipe parentPipe;

            public override bool CanRead => !IsDisposed;

            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException("This input stream doesn't support length, position, or seeking");

            public override long Position
            {
                get => throw new NotSupportedException("This input stream doesn't support length, position, or seeking");
                set => throw new NotSupportedException("This input stream doesn't support length, position, or seeking");
            }

            public override bool CanTimeout => false;

            public OutputStream(StreamPipe parentPipe)
            {
                this.parentPipe = parentPipe;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();

                ValidateBuffer(buffer, offset, count);

                return Task.Run(() => OnReadAsync(buffer, offset, count, CancellationToken.None)).Result;
            }

            protected override Task<int> OnReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                ThrowIfDisposed();

                ValidateBuffer(buffer, offset, count);

                return parentPipe.ReadMessageAsync(buffer, offset, count, cancellationToken);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException("This output stream doesn't support length, position, or seeking");
            }

            protected override void OnCloseAndDispose()
            {

            }
        }

        private class InputStream : InputStreamBase
        {
            private readonly StreamPipe parentPipe;

            public override bool CanSeek => false;

            public override bool CanWrite => !IsDisposed;

            public override long Length => throw new NotSupportedException("This input stream doesn't support length, position, or seeking");

            public override bool CanTimeout => false;

            public override long Position
            {
                get => throw new NotSupportedException("This input stream doesn't support length, position, or seeking");
                set => throw new NotSupportedException("This input stream doesn't support length, position, or seeking");
            }

            public InputStream(StreamPipe parentPipe)
            {
                this.parentPipe = parentPipe;
            }

            public override void Flush()
            {
                ThrowIfDisposed();
            }

            protected override Task OnFlushAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException("This input stream doesn't support length, position, or seeking");
            }

            // Let WriteAsync do the base logic of running this in a task
            public override void Write(byte[] buffer, int offset, int count)
            {
                ThrowIfDisposed();

                ValidateBuffer(buffer, offset, count);

                parentPipe.AddMessage(buffer, offset, count);
            }

            protected override void OnCloseAndDispose()
            {

            }
        }
    }
}
