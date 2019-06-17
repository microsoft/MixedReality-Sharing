using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Exposes methods to create and list rooms.
    /// </summary>
    public interface IMatchRoomManager
    {
        /// <summary>
        /// Find a room by its unique ID.
        /// </summary>
        /// <returns>a Task containing a null room if there are no rooms with the provided ID.</returns>
        Task<IMatchRoom> FindRoomByIdAsync(string roomId, CancellationToken token = default);

        /// <summary>
        /// Get the list of all rooms matching the given query.
        /// </summary>
        IMatchRoomList FindRooms(FindRoomQuery query);

        /// <summary>
        /// Create a new room and join it.
        /// </summary>
        /// <param name="properties">Properties to set on the new room.</param>
        /// <param name="token">
        /// If cancellation is requested, the method should either complete the operation and return a valid
        /// room, or roll back any changes to the system state and return a canceled Task.
        /// </param>
        /// <returns>
        /// The newly created, joined room, or a canceled task if the operation was canceled before completion.
        /// </returns>
        Task<IMatchRoom> CreateRoomAsync(Dictionary<string, object> properties = null, CancellationToken token = default);
    }

    /// <summary>
    /// Collection of the possible options to query rooms for.
    /// </summary>
    public class FindRoomQuery
    {
        /// <summary>
        /// Only find rooms with this owner.
        /// </summary>
        public IMatchParticipant owner;

        /// <summary>
        /// Only find rooms containing any of these contacts.
        /// </summary>
        public List<IMatchParticipant> members;

        /// <summary>
        /// Only find rooms containing all of these properties with the specified value.
        /// </summary>
        public Dictionary<string, object> properties;
    }

    /// <summary>
    /// Handle to the list of active matchmaking rooms that satisfy certain criteria.
    /// Can be used to either get the rooms at a specific point in time, or to subscribe and get updates
    /// as rooms get added/removed.
    /// </summary>
    public interface IMatchRoomList : INotifyCollectionChanged
    {
        /// <summary>
        /// Get the rooms that are active now.
        /// The implementation might return only a subset of the currently active rooms
        /// if the full set is too large.
        /// </summary>
        Task<IEnumerable<IMatchRoom>> GetRoomsAsync(CancellationToken token = default);
    }
}
