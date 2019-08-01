using System.Threading;

namespace Microsoft.MixedReality.Sharing
{
    public interface IParticipantProvider<out T>
        where T : IParticipant
    {
        T GetParticipantAsync(string id, CancellationToken cancellationToken);
    }
}
