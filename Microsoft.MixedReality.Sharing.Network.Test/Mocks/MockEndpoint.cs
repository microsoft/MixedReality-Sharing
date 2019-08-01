using Microsoft.MixedReality.Sharing.Core;

namespace Microsoft.MixedReality.Sharing.Network.Test.Mocks
{
    public class MockEndpoint : EndpointBase<MockSession, MockEndpoint>
    {
        public MockEndpoint(MockSession session)
            : base(session)
        {
        }
    }
}
