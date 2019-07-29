using Microsoft.MixedReality.Sharing.Network;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Exposes methods to create and list rooms.
    /// </summary>
    public interface IMatchmakingExtendedService
    {
        /// <summary>
        /// Get the list of all rooms with the specified owner.
        /// </summary>
        IRoomList FindRoomsByOwner(IParticipant owner);

        /// <summary>
        /// Get the list of all rooms containing any of the specified participants.
        /// </summary>
        IRoomList FindRoomsByParticipants(IEnumerable<IParticipant> participants);

        /// <summary>
        /// Get the list of all rooms containing all of these attributes with the specified value.
        /// Passing an empty dictionary will list all searchable rooms.
        /// </summary>
        IRoomList FindRoomsByAttributes(IDictionary<string, object> attributes);
    }

    /// <summary>
    /// Handle to the list of active matchmaking rooms that satisfy certain criteria.
    /// Can be used to:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// get the rooms at a specific point in time, asynchronously by calling
    /// <see cref="GetRoomsAsync(CancellationToken)"/></description>, or synchronously by enumeration this object;
    /// </item>
    /// <item>
    /// <description>
    /// subscribe and get updates as rooms get added/removed, through the `CollectionChanged` event.
    /// </description>
    /// </item>
    /// </list>
    /// Note that the implementation might prevent callers from subscribing to more than one room list
    /// at the same time.
    /// </summary>
    public interface IRoomList : IEnumerable<IRoom>, INotifyCollectionChanged, IDisposable
    {
        Task RefreshAsync(CancellationToken cancellationToken);
    }
}
