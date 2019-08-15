// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Handle to a joined matchmaking room.
    ///
    /// A room is a container for a Mixed Reality shared session, that can be created, advertised and/or joined through a
    /// matchmaking service. A participant joining/leaving a room will also join/leave the corresponding session.
    ///
    /// A process can use this interface to interact with a joined room and to access the corresponding session state.
    /// Instances of this interface are obtained when joining/creating a matchmaking room
    /// through an <see cref="IMatchmakingService"/> or <see cref="IRoomManager"/>. See <see cref="IRoomInfo"/> for the
    /// interface that wraps non-joined rooms.
    ///
    /// The lifetime of a room and the corresponding session is implementation-dependent.
    /// </summary>
    public interface IRoom
    {
        event Action RoomUpdated;

        string Id { get; }

        /// <summary>
        /// Current owner of this room. The owner is initially the participant who created the room.
        /// The implementation can choose a new owner if e.g. the current owner is disconnected.
        /// </summary>
        IParticipant Owner { get; }

        /// <summary>
        /// Gets a read-only attribute map associated with this room.
        /// </summary>
        IReadOnlyDictionary<string, string> Attributes { get; }

        /// <summary>
        /// Joins this room by creating and returning an ISession that is established.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop this operation.</param>
        /// <returns>An established session.</returns>
        Task<ISession> JoinAsync(CancellationToken cancellationToken);
    }

    public interface IOwnedRoom : IRoom, IDisposable
    {
        ISession Session { get; }

        void UpdateAttributes(Action<IDictionary<string, string>> updateCallback);
    }
}
