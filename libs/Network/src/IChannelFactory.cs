using MorseCode.ITask;
using System.Threading;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface IChannelFactory<TSessionType, out TMessageType>
        where TSessionType : class, ISession<TSessionType>
        where TMessageType : IMessage
    {
        string Name { get; }

        ITask<IChannel<TSessionType, TMessageType>> OpenChannelAsync(TSessionType session, CancellationToken cancellationToken);

        ITask<IChannel<TSessionType, TMessageType>> OpenChannelAsync(IEndpoint<TSessionType> endpoint, CancellationToken cancellationToken);
    }
}
