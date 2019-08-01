using MorseCode.ITask;
using System.Threading;

namespace Microsoft.MixedReality.Sharing
{
    public interface IChannelFactory<out TChannel>
        where TChannel : IChannel
    {
        string Name { get; }

        ITask<TChannel> OpenChannelAsync(ISession session, string channelId, CancellationToken cancellationToken);

        ITask<TChannel> OpenChannelAsync(IEndpoint endpoint, string channelId, CancellationToken cancellationToken);
    }
}
