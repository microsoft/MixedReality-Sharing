using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    /// <summary>
    /// Exposes methods to create and list rooms.
    /// </summary>
    public interface IRoomManager
    {
        /// <summary>
        /// Find a room by its unique ID.
        /// </summary>
        /// <returns>a <see cref="Task"/> containing a null room if there are no rooms with the provided ID.</returns>
        Task<IRoom> FindRoomByIdAsync(string roomId, CancellationToken token = default);

        /// <summary>
        /// Get the list of all rooms matching the given query.
        /// </summary>
        IRoomList FindRooms(FindRoomQuery query);

        /// <summary>
        /// Create a new room and join it.
        /// </summary>
        /// <param name="attributes">Attributes to set on the new room.</param>
        /// <param name="token">
        /// If cancellation is requested, the method should either complete the operation and return a valid
        /// room, or roll back any changes to the system state and return a canceled Task.
        /// </param>
        /// <returns>
        /// The newly created, joined room.
        /// </returns>
        Task<IRoom> CreateRoomAsync(Dictionary<string, object> attributes = null, CancellationToken token = default);
    }

    /// <summary>
    /// Collection of the possible options to query rooms for.
    /// </summary>
    public class FindRoomQuery
    {
        /// <summary>
        /// Only find rooms with this owner.
        /// </summary>
        public IParticipant Owner;

        /// <summary>
        /// Only find rooms containing any of these contacts.
        /// </summary>
        public List<IParticipant> Members;

        /// <summary>
        /// Only find rooms containing all of these attributes with the specified value.
        /// </summary>
        public Dictionary<string, object> Attributes;
    }

    /// <summary>
    /// Handle to the list of active matchmaking rooms that satisfy certain criteria.
    /// Can be used to either get the rooms at a specific point in time, or to subscribe and get updates
    /// as rooms get added/removed.
    /// Note that the implementation might prevent callers from subscribing to more than one room list
    /// at the same time.
    /// </summary>
    public interface IRoomList : INotifyCollectionChanged, IDisposable
    {
        /// <summary>
        /// Get the rooms that are active now.
        /// The implementation might return only a subset of the currently active rooms
        /// if the full set is too large.
        /// </summary>
        Task<IEnumerable<IRoom>> GetRoomsAsync(CancellationToken token = default);
    }
}
