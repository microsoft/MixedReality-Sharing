using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Network
{
    public interface INetService
    {
        event Action<IChannel, byte[]> MessageReceived;
    }
}
