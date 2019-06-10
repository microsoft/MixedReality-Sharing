using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Contact available for matchmaking.
    /// </summary>
    public interface IContact
    {
        string Id { get; }
        string DisplayName { get; }
        bool IsOnline { get; }
        //TODO others
    }

    /// <summary>
    /// Directory of contacts browsable by this process.
    /// </summary>
    public interface IContactService
    {
        /// <summary>
        /// Return the contacts whose ID or DisplayName match the regex.
        /// </summary>
        Task<IEnumerable<IContact>> SearchAsync(string regex, CancellationToken cancellationToken);

        /// <summary>
        /// Get the details for the contact with the given ID.
        /// </summary>
        Task<IContact> GetContactAsync(string id, CancellationToken cancellationToken);

        /// <summary>
        /// Get the details corresponding to the user associated to this service instance.
        /// </summary>
        Task<IContact> GetUserContactAsync();
    }
}
