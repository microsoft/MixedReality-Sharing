using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Utilities
{
    public ref struct RefreshEventToken
    {
        private readonly Action applyRefreshAction;

        internal RefreshEventToken(Action applyRefreshAction)
        {
            this.applyRefreshAction = applyRefreshAction ?? throw new ArgumentNullException(nameof(applyRefreshAction));
        }

        public void ApplyRefresh()
        {
            applyRefreshAction?.Invoke();
        }
    }

    public delegate void RefreshReadyEventHandler(RefreshEventToken refreshToken);

    public interface IRefreshableCollection<out T> : IReadOnlyCollection<T>, IDisposable
    {
        event RefreshReadyEventHandler RefreshReady;
    }

    public abstract class RefreshableCollectionBase<T> : DisposableBase, IRefreshableCollection<T>
    {
        public event RefreshReadyEventHandler RefreshReady;

        private readonly Queue<Action> updates = new Queue<Action>();
        private readonly SynchronizationContext synchronizationContext;

        private volatile bool refreshQueued = false;

        public abstract int Count { get; }

        protected RefreshableCollectionBase(SynchronizationContext synchronizationContext)
        {
            this.synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
        }

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected void QueueUpdate(Action action)
        {
            ThrowIfDisposed();

            lock (updates)
            {
                if (RefreshReady == null)
                {
                    // We will only queue refreshes if the event is listened to, otherwise just process them
                    action();
                }
                else
                {
                    updates.Enqueue(action);

                    if (!refreshQueued)
                    {
                        Task.Run(() => PerformRefreshAsync(DisposeCancellationToken), DisposeCancellationToken).FireAndForget();
                        refreshQueued = true;
                    }
                }
            }
        }

        protected override void OnManagedDispose()
        {
            lock (updates)
            {
                updates.Clear();
            }
        }

        private async Task PerformRefreshAsync(CancellationToken cancellationToken)
        {
            if (synchronizationContext != null)
            {
                await synchronizationContext.AsTask(cancellationToken);
            }

            // The callback has to happen synchronously
            RefreshReady?.Invoke(new RefreshEventToken(() =>
            {
                if (refreshQueued)
                {
                    lock (updates)
                    {
                        while (updates.Count > 0)
                        {
                            updates.Dequeue().Invoke();
                        }

                        refreshQueued = false;
                    }
                }
            }));
        }
    }
}
