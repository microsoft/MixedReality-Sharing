// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// An immutable object that represents a change to the state.
    /// Can be applied to a state to transition it to the next version.
    /// </summary>
    public class Transaction : Utilities.VirtualRefCountedBase
    {
        public enum Outcome
        {
            /// <summary>
            /// The transaction was successfully applied, and the version was incremented.
            /// </summary>
            /// <remarks>The outcome means that all prerequisites were satisfied,
            /// and all requested changes were effectively made. However, it doesn't
            /// mean that the number of modifications is the same as requested
            /// (and can even be zero).
            /// For example, if some subkey already had a value X, a request to
            /// write X to that subkey won't generate an edit, but the overall
            /// outcome of the transaction will be successful.
            /// If this is not the desired behavior, use explicit requirements
            /// while building the transaction. </remarks>
            Applied,

            /// <summary>
            /// The operations in the transaction couldn't be applied due to unsatisfied
            /// prerequisites, but the version was incremented anyway.
            /// </summary>
            PrerequisitesFailed,

            /// <summary>
            /// The transaction couldn't be persisted (usually because of the connectivity
            /// issues), and therefore it wasn't applied.
            /// </summary>
            FailedToPersist,

            /// <summary>
            /// The outcome of the transaction is unknown, usually because the connection
            /// was lost after the attempt to commit the transaction was made.
            /// </summary>
            Unknown,

            /// <summary>
            /// The transaction couldn't be applied due to insufficient resources.
            /// The version is not incremented since this this result could be specific
            /// to this machine and not be reproducible under different conditions (and
            /// thus incrementing the version could make the behavior non-deterministic),
            /// and no further modifications can be made to the same state.
            /// Old snapshots can still be safely observed. 
            /// </summary>
            InsufficientResources,
        }

        // TODO: there should be multiple constructors for trivial transactions,
        // and only the complex ones should require the TransactionBuilder.

        internal Transaction(IntPtr handle)
        {
            this.handle = handle;
        }
    }
}
