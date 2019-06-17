using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Participant available for matchmaking.
    /// Can correspond to a user or to a device depending on the matchmaking implementation.
    /// </summary>
    public interface IParticipant
    {
        string Id { get; }
        string DisplayName { get; }
        bool IsOnline { get; }
    }

    /// <summary>
    /// Gets a participant object from an ID.
    /// </summary>
    public interface IParticipantFactory
    {
        /// <summary>
        /// Gets a participant object from an ID.
        /// </summary>
        Task<IParticipant> GetParticipantAsync(string id, CancellationToken cancellationToken);

        /// <summary>
        /// ID of the matchmaking participant corresponding to the local user.
        /// </summary>
        string LocalParticipantId { get; }
    }
}
