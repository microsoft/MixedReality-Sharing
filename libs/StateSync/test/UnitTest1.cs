using System;
using Xunit;

namespace Microsoft.MixedReality.Sharing.StateSync.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var snap = new LightweightSnapshot();
            //Assert.NotNull();
        }

        [Theory]
        [InlineData("p")]
        [InlineData("O;Malley")]
        [InlineData("O,Malley")]
        public void TestWithHardcodedParams(string name)
        {
            Assert.Equal(-1, name.IndexOf('x'));
        }
    }
}
