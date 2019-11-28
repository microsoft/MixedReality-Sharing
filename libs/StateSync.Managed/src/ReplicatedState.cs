// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// Versioned snapshottable two-level {key:subkey}=>value map
    /// that is synchronized over the user-provided transport.
    /// </summary>
    public class ReplicatedState : Utilities.VirtualRefCountedBase
    {
        /// <summary>
        /// The unique identifier of the replicated state.
        /// </summary>
        /// <remarks>Must be provided in the constructor for the <see cref="ReplicatedState"/>.</remarks>
        public readonly Guid StateGuid;

        /// <summary>
        /// A unique identifier that is generated each time a new <see cref="ReplicatedState"/> object is created.
        /// Identifies this particular instance of the <see cref="ReplicatedState"/> class.
        /// </summary>
        /// <remarks>This Guid will be attached to all transactions committed by this instance, and
        /// then will be passed as an argument to the various methods of <see cref="UpdateListener"/>
        /// when <see cref="ProcessSingleUpdate(UpdateListener)"/> is called.
        /// This way the listener can tell its own transactions apart from similar transactions committed
        /// by other instances (typically located on other devices).</remarks>
        public readonly Guid InstanceGuid = Guid.NewGuid();

        /// <summary>
        /// Constructs a new state.
        /// </summary>
        /// <param name="stateGuid">The unique identifier of the replicated state.</param>
        /// <remarks>After a new ReplicatedState is created, the initial version is 0,
        /// and the state is empty.</remarks>
        /// TODO: the transport layer should also be provided as an argument.
        public ReplicatedState(Guid stateGuid) {
            StateGuid = stateGuid;
            // TODO: use the guids (this is a placeholder code)
            handle = PInvoke_Create();
        }

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        /// <return>The local incremental ID of the commit that is sent together with <see cref="InstanceGuid"/>.</return>
        /// <remarks>The method is non-blocking and will return immediately after the transaction is scheduled.
        /// Calling <see cref="ProcessSingleUpdate(UpdateListener)"/> repeatedly may eventually trigger
        /// a callback of an <see cref="UpdateListener"/> related to this commit,
        /// assuming that this instance wasn't disconnected for a substantial amount of time,
        /// in which case the history of commits may be lost, and the state may be advanced past this
        /// transaction via <see cref="UpdateListener.OnStateAdvanced(OnStateAdvancedArgs)"/>.</remarks>
        public ulong Commit(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Processes a single pending update from the queue, or returns immediately
        /// if there are no pending updates.
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
        public (StateSnapshot, bool) ProcessSingleUpdate(UpdateListener listener)
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
