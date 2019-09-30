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
        public static IEnumerable<IRoom> QueryAndWaitForRoomsPredicate(
            IMatchmakingService svc, string type,
            Func<IEnumerable<IRoom>, bool> pred, CancellationToken token)
        {
            using (var result = svc.StartDiscovery(type))
            {
                var rooms = result.Rooms;
                if (pred(rooms))
                {
                    return rooms; // optimistic path
                }
                if (token.IsCancellationRequested)
                {
                    return null;
                }
                using (var wakeUp = new AutoResetEvent(false))
                {
                    Action<IDiscoveryTask> onChange = (IDiscoveryTask sender) => wakeUp.Set();

                    using (var unregisterCancel = token.Register(() => wakeUp.Set()))
                    using (var unregisterWatch = new RaiiGuard(() => result.Updated += onChange, () => result.Updated -= onChange))
                    {
                        while (true)
                        {
                            wakeUp.WaitOne(); // wait for cancel or update
                            rooms = result.Rooms;
                            if (pred(rooms))
                            {
                                return rooms;
                            }
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
}
