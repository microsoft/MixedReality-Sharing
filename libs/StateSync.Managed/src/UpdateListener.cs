// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.StateSync.Snapshots;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public readonly ref struct OnStateAdvancedArgs
    {
        /// WIP: incomplete.


        /// <summary>
        /// The snapshot of the state before it was advanced.
        /// </summary>
        public readonly StateSnapshot snapshotBefore;

        /// <summary>
        /// The snapshot of the state after it was advanced.
        /// </summary>
        /// <remarks>The version of this snapshot is always
        /// at least 1 greater than the version of <see cref="snapshotBefore"/>.</remarks>
        public readonly StateSnapshot snapshotAfter;
    }

    public readonly ref struct OnTransactionAppliedArgs
    {
        /// WIP: incomplete.

        /// <summary>
        /// senderId field that was originally provided by the sender of this transaction.
        /// </summary>
        public readonly ulong senderId;

        /// <summary>
        /// senderEntryId field that was originally provided by the sender of this transaction
        /// </summary>
        public readonly ulong senderEntryId;

        /// <summary>
        /// The snapshot of the state before the transaction was applied.
        /// </summary>
        public readonly StateSnapshot snapshotBefore;

        /// <summary>
        /// The snapshot of the state after the transaction was applied.
        /// </summary>
        /// <remarks>The version of this snapshot is always 1 greater than the version of <see cref="snapshotBefore"/>.</remarks>
        public readonly StateSnapshot snapshotAfter;

        /// <summary>
        /// The collection of keys that were inserted as the result of the transaction.
        /// </summary>
        public readonly UpdatedKeysCollection insertedKeys;

        /// <summary>
        /// The collection of keys that were updated as the result of the transaction.
        /// </summary>
        public readonly UpdatedKeysCollection updatedKeys;

        /// <summary>
        /// The collection of keys that were removed as the result of the transaction.
        /// </summary>
        public readonly UpdatedKeysCollection removedKeys;
    }
    public readonly ref struct OnPrerequisitesFailedArgs
    {
        /// WIP: incomplete.

        /// <summary>
        /// senderId field that was originally provided by the sender of this transaction.
        /// </summary>
        public readonly ulong senderId;

        /// <summary>
        /// senderEntryId field that was originally provided by the sender of this transaction
        /// </summary>
        public readonly ulong senderEntryId;
        /// <summary>

        /// <summary>
        /// The snapshot of the state before the transaction was applied.
        /// </summary>
        public readonly StateSnapshot snapshotBefore;

        /// <summary>
        /// The snapshot of the state after the transaction was applied.
        /// </summary>
        /// <remarks>The version of this snapshot is always 1 greater than the version of <see cref="snapshotBefore"/>,
        /// but otherwise it is identical to it.</remarks>
        public readonly StateSnapshot snapshotAfter;
    }


    /// <summary>
    /// Implementations of this interface can be passed to <see cref="ReplicatedState.ProcessUpdates"/>
    /// to react to modifications that happened to the <see cref="ReplicatedState"/>.
    /// </summary>
    /// TODO: discuss what's the scope of this interface, and which parts of the workflow should
    /// be handled by other mechanisms.
    public interface UpdateListener
    {
        /// <summary>
        /// Invoked when the local state gets advanced to a newer state without the history of transaction.
        /// </summary>
        /// <remarks>The listener won't receive other notifications for transactions
        /// that advanced the state this way. Typically this happens when the listener
        /// reconnects to the server after some time, and the server can't advance the
        /// state known to the client via individual transactions because the history
        /// of transactions has been trimmed, or because the listener starts from
        /// version 0 (initial empty state).</remarks>
        void OnStateAdvanced(OnStateAdvancedArgs args);

        /// <summary>
        /// Invoked when the transaction is successfully applied.
        /// </summary>
        void OnTransactionApplied(OnTransactionAppliedArgs args);

        /// <summary>
        /// Invoked when the transaction wasn't applied due to unsatisfied prerequisites.
        /// </summary>
        void OnPrerequisitesFailed(OnPrerequisitesFailedArgs args);

        // TODO: discuss which events we want to process by this class.
        //void OnFailedToPersist(Transaction transaction);
    }
}
