// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Versioned snapshottable two-level {key:subkey}=>value map
    /// that is synchronized over the user-provided transport.
    /// </summary>
    public class ReplicatedState : Utilities.VirtualRefCountedBase
    {
        /// <summary>
        /// Constructs a new state.
        /// </summary>
        /// <remarks>The initial version is 0, and the state is empty.</remarks>
        /// TODO: the transport layer should also be provided as an argument.
        public ReplicatedState(Guid guid) {
            // TODO: use guid (this is a placeholder code)
            handle = PInvoke_Create();
        }

        // Why is this commented out:
        // to make application of the transactions independent from processing their triggers,
        // the user should use the workflow described in ProcessUpdates().
        // This way transactions can be applied by the background thread, while the main
        // thread of the application advances the state as fast as it can without dropping
        // frames. This way a sudden burst of state modifications won't decrease the performance.

        //         /// <summary>
        //         /// Returns the latest snapshot of this state.
        //         /// </summary>
        //         /// <returns>An immutable snapshot representing the current version of the state.</returns>
        //         public unsafe StateSnapshot GetSnapshot()
        //         {
        //             return new StateSnapshot(this, handle);
        //         }

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        public Action<Transaction.Outcome> Commit(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        // TODO: discuss:
        // It feels like the clients don't necessarily need the Action-based interface,
        // especially if we want to separate the moment the transaction
        // is applied and the moment the updates are processed (see below).
        public void CommitSilently(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes some pending updates that are already validated and applied to the state.
        /// </summary>
        /// <returns>The snapshot of the state after the last processed update,
        /// and a flag that indicates whether there are more unprocessed updates in the queue.
        /// </returns>
        /// <remarks>
        /// This method should be used in a loop on each frame's update step,
        /// until either there are no more pending updates, or there is not enough
        /// time left to process the remaining updates in this frame.
        /// The caller can stop at any point, and resume processing the transactions
        /// on the next opportunity (for example, on the next frame's update step).
        /// </remarks>
        public (StateSnapshot, bool) ProcessUpdates(UpdateListener listener)
        {
            throw new NotImplementedException();
        }

        // Why is this commented out:
        // The exact shape of the subscriptions is WIP.

        ///// <summary>
        ///// Registers a subscription to changes related to the provided key.
        ///// </summary>
        ///// <param name="key">The key for which to register the subscription.</param>
        ///// <param name="subscription">The subscription instance.</param>
        ///// <returns>A token to unregister the subscription.</returns>
        //public SubscriptionToken SubscribeToKey(KeyRef key, IKeySubscription subscription)
        //{
        //    throw new NotImplementedException();
        //}

        ///// <summary>
        ///// Registers a key and subkey subscription to the storage.
        ///// </summary>
        ///// <param name="key">The key for which to register the subscription.</param>
        ///// <param name="subkey">The subkey for which to register the subscription.</param>
        ///// <param name="subscription">The subscription instance.</param>
        ///// <returns>A token to unregister the subscription.</returns>
        //public SubscriptionToken SubscribeToKey(KeyRef key, ulong subkey, ISubkeySubscription subscription)
        //{
        //    throw new NotImplementedException();
        //}

        // Returns a handle to the freshly created ReplicatedState.
        [DllImport(PInvokeAPI.LibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_ReplicatedState_Create")]
        private static extern IntPtr PInvoke_Create();
    }
}
