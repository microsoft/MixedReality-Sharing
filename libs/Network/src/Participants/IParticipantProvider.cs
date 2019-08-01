// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;

namespace Microsoft.MixedReality.Sharing
{
    /// <summary>
    /// A provider to be implemented by consuming code to inflate participants from identifiers.
    /// </summary>
    /// <typeparam name="T">The type of participant.</typeparam>
    public interface IParticipantProvider<out T>
        where T : IParticipant
    {
        /// <summary>
        /// Asynchronously resolves a participant based on given identifier.
        /// </summary>
        /// <param name="id">The identifier of the participant.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to interrupt early.</param>
        /// <returns>The inflated participant.</returns>
        T GetParticipantAsync(string id, CancellationToken cancellationToken);
    }
}
