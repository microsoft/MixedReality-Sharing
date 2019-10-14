// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Test
{
    public static class Utils
    {
        public static int TestTimeoutMs
        {
            get
            {
                return Debugger.IsAttached ? Timeout.Infinite : 10000;
            }
        }

        public static void AssertSameDictionary(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        {
            Assert.Equal(a.Count, b.Count);
            foreach (var entry in a)
            {
                Assert.Equal(entry.Value, b[entry.Key]);
            }
        }

        public class RaiiGuard : IDisposable
        {
            private Action Quit { get; set; }
            public RaiiGuard(Action init, Action quit)
            {
                Quit = quit;
                if (init != null) init();
            }
            void IDisposable.Dispose()
            {
                if (Quit != null) Quit();
            }
        }

        // Run a query and wait for the predicate to be satisfied.
        // Return the list of resources which satisfied the predicate or null if canceled before the predicate was satisfied.
        public static IEnumerable<IDiscoveryResource> QueryAndWaitForResourcesPredicate(
            IDiscoveryAgent svc, string type,
            Func<IEnumerable<IDiscoveryResource>, bool> pred, CancellationToken token)
        {
            using (var discovery = svc.Subscribe(type))
            {
                return QueryAndWaitForResourcesPredicate(discovery, pred, token);
            }
        }

        // Run a query and wait for the predicate to be satisfied.
        // Return the list of resources which satisfied the predicate or null if canceled before the predicate was satisfied.
        public static IEnumerable<IDiscoveryResource> QueryAndWaitForResourcesPredicate(
            IDiscoverySubscription discovery, Func<IEnumerable<IDiscoveryResource>, bool> pred, CancellationToken token)
        {
            // Check optimistically before subscribing to the discovery event.
            var resources = discovery.Resources;
            if (pred(resources))
            {
                return resources;
            }
            if (token.IsCancellationRequested)
            {
                return null;
            }
            using (var wakeUp = new AutoResetEvent(false))
            {
                Action<IDiscoverySubscription> onChange = (IDiscoverySubscription sender) => wakeUp.Set();

                using (var unregisterCancel = token.Register(() => wakeUp.Set()))
                using (var unregisterWatch = new RaiiGuard(() => discovery.Updated += onChange, () => discovery.Updated -= onChange))
                {
                    while (true)
                    {
                        // Check before waiting on the event so that updates aren't missed.
                        resources = discovery.Resources;
                        if (pred(resources))
                        {
                            return resources;
                        }
                        wakeUp.WaitOne(); // wait for cancel or update
                        if (token.IsCancellationRequested)
                        {
                            return null;
                        }
                    }
                }
            }
        }
    }
}
