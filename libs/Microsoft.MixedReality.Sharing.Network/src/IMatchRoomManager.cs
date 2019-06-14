using System;
using System.Collections.Generic;
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
        Task<IEnumerable<IMatchRoom>> FindRoomsAsync(FindRoomQuery query, CancellationToken token = default);

        /// <summary>
        /// Start listening for the list of rooms. After this is called, the list of current joinable rooms
        /// will be queryable through <see cref="RoomListUpdated"/>.
        /// </summary>
        // todo: do we want multiple subscriptions?
        void SubscribeToRooms(FindRoomQuery query);

        /// <summary>
        /// Stop listening for the list of rooms.
        /// </summary>
        void UnsubscribeFromRooms();

        /// <summary>
        /// If the process has subscribed to the room list with SubscribeToRooms, this triggers when the current list
        /// of joinable room changes. The exact frequency depends on the implementation.
        /// </summary>
        event Action<IEnumerable<IMatchRoom>> RoomListUpdated;

        /// <summary>
        /// Create a new room and join it.
        /// </summary>
        /// <param name="properties">Properties to set on the new room.</param>
        /// <param name="token">
        /// Used to check for cancellation.
        /// Whether and how cancellation is handled is up to the implementation.
        /// </param>
        /// <returns>The ISession corresponding to the joined room.</returns>
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
}
