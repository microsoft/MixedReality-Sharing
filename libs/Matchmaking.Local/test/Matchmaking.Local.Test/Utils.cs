// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace Matchmaking.Local.Test
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
        // Return the list of rooms which satisfied the predicate or null if canceled before the predicate was satisfied.
        public static IEnumerable<IDiscoveryResource> QueryAndWaitForRoomsPredicate(
            IDiscoveryAgent svc, string type,
            Func<IEnumerable<IDiscoveryResource>, bool> pred, CancellationToken token)
        {
            using (var discovery = svc.Subscribe(type))
            {
                return QueryAndWaitForRoomsPredicate(discovery, pred, token);
            }
        }

        // Run a query and wait for the predicate to be satisfied.
        // Return the list of rooms which satisfied the predicate or null if canceled before the predicate was satisfied.
        public static IEnumerable<IDiscoveryResource> QueryAndWaitForRoomsPredicate(
            IDiscoverySubscription discovery, Func<IEnumerable<IDiscoveryResource>, bool> pred, CancellationToken token)
        {
            // Check optimistically before subscribing to the discovery event.
            var rooms = discovery.Rooms;
            if (pred(rooms))
            {
                return rooms;
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
                        rooms = discovery.Rooms;
                        if (pred(rooms))
                        {
                            return rooms;
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
