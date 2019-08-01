using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing
{
    public static class Extensions
    {
        public static async Task<TChannel> GetChannelAsync<TChannel>(this ISession session, CancellationToken cancellationToken)
            where TChannel : IChannel
        {
            return await session.GetChannelAsync<TChannel>(null, cancellationToken);
        }

        public static async Task<TChannel> GetChannelAsync<TChannel>(this IEndpoint endpoint, CancellationToken cancellationToken)
            where TChannel : IChannel
        {
            return await endpoint.GetChannelAsync<TChannel>(null, cancellationToken);
        }
    }
}
