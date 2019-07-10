using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Xunit;

namespace Microsoft.MixedReality.Sharing.StateSync.Test
{
    class EmptySnapshot : ISnapshot
    {
        public int Version { get { return 0; } }
        public ulong KeysCount {  get { return 0; } }
        public ulong SubkeysCount {  get { return 0; } }

        public ulong GetSubkeysCount(IKey key) { return 0; }
        public bool Contains(IKey key, ulong subkey) { return false; }

        public bool Get(IKey key, ulong subkey, out byte[] value) { value = null; return false; }
        public bool Get(IKey key, ulong subkey, out object value) { value = null; return false; }

        public IEnumerable<SubkeyValue> CreateSubkeyEnumerator(IKey key, ulong min_key=0)
        {
            return ImmutableArray<SubkeyValue>.Empty;
        }
        public IEnumerable<IKey> CreateKeyEnumerator()
        {
            return ImmutableArray<IKey>.Empty;
        }

        public void Dispose() { }
    }

    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            var snap = new EmptySnapshot();
            Assert.NotNull(snap.GetType());
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
