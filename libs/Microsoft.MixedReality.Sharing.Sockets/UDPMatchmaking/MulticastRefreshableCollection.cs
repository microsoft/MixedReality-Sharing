using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Sockets.UDPMatchmaking
{
    internal class MulticastRefreshableCollection : RefreshableCollectionBase<UDPMulticastRoom>
    {
        private readonly Dictionary<Guid, UDPMulticastRoom> roomCollection = new Dictionary<Guid, UDPMulticastRoom>();
        private readonly Func<UDPMulticastRoom, bool> queryPredicate;

        internal MulticastRefreshableCollection(Func<UDPMulticastRoom, bool> queryPredicate, SynchronizationContext synchronizationContext)
            : base(synchronizationContext)
        {
            this.queryPredicate = queryPredicate ?? throw new ArgumentNullException(nameof(queryPredicate));
        }

        public override int Count => roomCollection.Count;

        public override IEnumerator<UDPMulticastRoom> GetEnumerator()
        {
            return roomCollection.Values.GetEnumerator();
        }

        internal void CheckForUpdate(UDPMulticastRoom room)
        {
            if (room.State != UDPMulticastRoomState.Ready)
            {
                // Another update will be scheduled later anyways
                return;
            }
            else if (roomCollection.ContainsKey(room.Id))
            {
                // Check for deletion if query is no longer satisfied
                if (!queryPredicate(room))
                {
                    // Queue removal
                    QueueUpdate(() => roomCollection.Remove(room.Id));
                }
            }
            else
            {
                // Check for addition if query is satisfied
                if (queryPredicate(room))
                {
                    QueueUpdate(() => roomCollection.Add(room.Id, room));
                }
            }
        }
    }
}
