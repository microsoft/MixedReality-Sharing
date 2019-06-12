using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Participant available for matchmaking.
    /// Can correspond to a user or to a device depending on the matchmaking implementation.
    /// </summary>
    public interface IMatchParticipant
    {
        string Id { get; }
        string DisplayName { get; }
        bool IsOnline { get; }
    }

    /// <summary>
    /// Gets a participant object from an ID.
    /// </summary>
    public interface IMatchParticipantFactory
    {
        /// <summary>
        /// Gets a participant object from an ID.
        /// </summary>
        Task<IMatchParticipant> GetParticipantAsync(string id, CancellationToken cancellationToken);

        /// <summary>
        /// ID of the matchmaking participant corresponding to the local user.
        /// </summary>
        string LocalParticipantId { get; }
    }
}
