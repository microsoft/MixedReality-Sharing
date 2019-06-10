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
    public interface IRoomManager
    {
        /// <summary>
        /// Find a room by its unique ID.
        /// </summary>
        /// <returns>null if there are no rooms with the provided ID.</returns>
        Task<IRoom> FindRoomByIdAsync(string roomId, CancellationToken cancellationToken);

        /// <summary>
        /// Get the list of all rooms matching the given query.
        /// </summary>
        Task<IEnumerable<IRoom>> FindRoomsAsync(FindRoomQuery query, CancellationToken cancellationToken);

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
        event Action<IEnumerable<IRoom>> RoomListUpdated;

        /// <summary>
        /// Create a new room and join it.
        /// </summary>
        /// <param name="roomId">ID of the new room. Must be unique.</param>
        /// <param name="properties">Properties to set on the new room.</param>
        /// <param name="reservedContacts">
        /// The method will reserve slots for these contacts in the created room.
        /// Depending on the implementation, the method might return a joined IRoom immediately
        /// or after all contacts have joined the room.
        /// </param>
        /// <returns>The ISession corresponding to the joined room.</returns>
        Task<IRoom> CreateRoomAsync(string roomId, RoomProperties properties = null, IEnumerable<IContact> reservedContacts = null);
    }

    /// <summary>
    /// Collection of the possible options to query rooms for.
    /// </summary>
    public class FindRoomQuery
    {
        /// <summary>
        /// Only find rooms with this owner.
        /// </summary>
        public IContact owner;

        /// <summary>
        /// Only find rooms containing any of these contacts.
        /// </summary>
        public List<IContact> members;

        /// <summary>
        /// Only find rooms containing all of these properties with the specified value.
        /// </summary>
        public RoomProperties properties;
    }
}
