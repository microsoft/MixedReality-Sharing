// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Utilities.IO
{
    public abstract class StreamBase : Stream
    {
        protected static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            else if (buffer.Length <= offset || offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            else if (buffer.Length < offset + count || count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        private readonly object lockObject = new object();
        private readonly CancellationTokenSource disposedCTS = new CancellationTokenSource();
        private bool isDisposed;

        protected CancellationToken DisposalToken { get; }

        public bool IsDisposed
        {
            get
            {
                lock (lockObject)
                {
                    return isDisposed;
                }
            }
        }

        protected StreamBase()
        {
            DisposalToken = disposedCTS.Token;
        }

        ~StreamBase()
        {
            Dispose(false);
        }

        public sealed override void Close()
        {
            lock (lockObject)
            {
                if (IsDisposed)
                {
                    return;
                }

                OnCloseAndDispose();

                // This will propogate to Dispose(bool)
                base.Close();

                disposedCTS.Cancel();
                disposedCTS.Dispose();
                isDisposed = true;
            }
        }

        public sealed override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken))
            {
                await OnCopyToAsync(destination, bufferSize, cts.Token);
            }
        }

        public sealed override async Task FlushAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken))
            {
                await OnFlushAsync(cts.Token);
            }
        }

        public sealed override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken))
            {
                return await OnReadAsync(buffer, offset, count, cts.Token);
            }
        }

        public sealed override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DisposalToken))
            {
                await OnWriteAsync(buffer, offset, count, cts.Token);
            }
        }

        protected virtual Task OnCopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        protected virtual Task OnFlushAsync(CancellationToken cancellationToken)
        {
            return base.FlushAsync(cancellationToken);
        }

        protected virtual Task<int> OnReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.ReadAsync(buffer, offset, count, cancellationToken);
        }

        protected virtual Task OnWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        protected sealed override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                OnUnmanagedDispose();
            }
        }

        protected void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        protected virtual void OnUnmanagedDispose() { }
        protected abstract void OnCloseAndDispose();
    }
}
