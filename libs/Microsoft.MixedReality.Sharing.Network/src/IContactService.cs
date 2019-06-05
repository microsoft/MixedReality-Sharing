using System;
using System.Collections.Generic;
using System.Text;
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
        Task<IEnumerable<IContact>> SearchAsync(string query);
        Task<IContact> GetContactAsync(string id);
        Task<IContact> GetMyContactAsync();
    }
}
