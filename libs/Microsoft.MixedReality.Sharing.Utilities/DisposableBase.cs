// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Utilities
{
    /// <summary>
    /// This is a base class for common IDisposable implementation.
    /// </summary>
    /// <remarks>Follows https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose </remarks>
    public class DisposableBase : IDisposable
    {
        private readonly CancellationTokenSource disposeCTS = new CancellationTokenSource();
        private ThreadLocal<bool> insideDisposeFunction = new ThreadLocal<bool>(() => false);
        private string objectName;

        /// <summary>
        /// Is the current object disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// The synchronization object to lock on.
        /// </summary>
        protected object LockObject { get; } = new object();

        /// <summary>
        /// A helper token that can be used to listen for Dispose to happen.
        /// </summary>
        protected CancellationToken DisposeCancellationToken { get; }

        /// <summary>
        /// The name of the current object.
        /// </summary>
        protected virtual string ObjectName
        {
            get
            {
                lock (LockObject)
                {
                    return objectName ?? (objectName = GetType().Name);
                }
            }
        }

        ~DisposableBase()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose the current object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            lock (LockObject)
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;

                // If the finalizer is running, don't access the insideDisposeFunction, as it will
                // also be finalizing.
                if (!isDisposing)
                {
                    insideDisposeFunction = null;
                }
                else
                {
                    insideDisposeFunction.Value = true;
                }
            }

            try
            {
                if (isDisposing)
                {
                    disposeCTS.Cancel();
                    disposeCTS.Dispose();
                    OnManagedDispose();
                }

                OnUnmanagedDispose();
            }
            catch (Exception ex)
            {
                // Inside finalizer don't rethrow the exception
                if (!isDisposing)
                {
                    LoggingUtility.LogError($"Unhandled exception inside Dispose function of {GetType().Name}.", ex);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (insideDisposeFunction != null)
                {
                    insideDisposeFunction.Value = false;
                }
            }
        }

        /// <summary>
        /// Override this method to dispose of managed objects.
        /// </summary>
        protected virtual void OnManagedDispose() { }

        /// <summary>
        /// Override this method to dispose of unmanaged objects.
        /// </summary>
        protected virtual void OnUnmanagedDispose() { }

        /// <summary>
        /// A helper method to throw if the current object is disposed.
        /// </summary>
        public void ThrowIfDisposed()
        {
            lock (LockObject)
            {
                if (insideDisposeFunction == null || (!insideDisposeFunction.Value && IsDisposed))
                {
                    throw new ObjectDisposedException(ObjectName);
                }
            }
        }
    }
}
