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

    public interface IRefreshableCollection<out T> : IReadOnlyCollection<T>
    {
        event RefreshReadyEventHandler RefreshReady;
    }

    public abstract class RefreshableCollectionBase<T> : IRefreshableCollection<T>
    {
        public event RefreshReadyEventHandler RefreshReady;

        private readonly SynchronizationContext synchronizationContext;

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

        protected async Task PerformRefreshAsync(Action updateCallback)
        {
            await synchronizationContext;

            bool updated = false;
            // Happens synchronously
            RefreshReady?.Invoke(new RefreshEventToken(() =>
            {
                if (!updated)
                {
                    updated = true;
                    updateCallback();
                }
            }));
        }
    }
}
