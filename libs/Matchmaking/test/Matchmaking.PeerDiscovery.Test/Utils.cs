// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
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

        // Utility that captures any exception thrown by a block of code to throw it later.
        public class DelayedException
        {
            private Exception exception_;
            public void Run(Action action)
            {
                try
                {
                    action();
                }
                catch(Exception e)
                {
                    exception_ = e;
                }
            }

            public Action Wrap(Action action)
            {
                return () => { Run(action); };
            }
            public void Run<T1>(Action<T1> action, T1 t1)
            {
                try
                {
                    action(t1);
                }
                catch (Exception e)
                {
                    exception_ = e;
                }
            }

            public Action<T1> Wrap<T1>(Action<T1> action)
            {
                return (t1) => { Run(action, t1); };
            }
            public void Run<T1, T2>(Action<T1, T2> action, T1 t1, T2 t2)
            {
                try
                {
                    action(t1, t2);
                }
                catch (Exception e)
                {
                    exception_ = e;
                }
            }

            public Action<T1, T2> Wrap<T1, T2>(Action<T1, T2> action)
            {
                return (t1, t2) => { Run(action, t1, t2); };
            }

            // Add more if necessary

            public void Rethrow()
            {
                if (exception_ != null)
                {
                    ExceptionDispatchInfo.Capture(exception_).Throw();
                }
            }
        }
    }
}
