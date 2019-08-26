using System;
using Xunit;

namespace Microsoft.MixedReality.Sharing.StateSync.Test
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            Assert.NotNull(typeof(Snapshot));
        }

        // This method is just to ensure compilation at this stage, will be replaced with implementation later
        internal void EnsureCompilation()
        {
            Snapshot s = default;
            foreach (SubkeyValuePair pair in s.GetSubkeys(default))
            {
                ulong subkey = pair.Subkey;
                ReadOnlySpan<byte> value = pair.Value;

                // Some dumb thing just to have this here for compiler
                Assert.True(value.Length + (long)subkey > 0);
            }

            foreach (KeyRef key in s.Keys)
            {
                Assert.True(key.AsSearchKey() != null);
            }
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
