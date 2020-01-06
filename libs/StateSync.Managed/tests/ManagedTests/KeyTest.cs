// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.MixedReality.Sharing.StateSync.Test
{
    public class KeyTest
    {
        static byte[] bytesFoo = new byte[] { 102, 111, 111 };  // "foo"
        static byte[] bytesNihao = new byte[] { 228, 189, 160, 229, 165, 189 };  // "你好" in UTF-8
        static byte[] bytesWithZeros = new byte[] { 1, 100, 2, 200, 0, 255, 0, 255 };

        [Fact]
        public void Create()
        {
            // At least for the cases when UTF-8 conversion round trips.
            Key keyFoo = new Key("foo");
            Assert.Equal("foo", keyFoo.ToString());
            Assert.True(keyFoo.ToSpan().SequenceEqual(bytesFoo));

            Key keyNihao = new Key("你好");
            Assert.Equal("你好", keyNihao.ToString());
            Assert.True(keyNihao.ToSpan().SequenceEqual(bytesNihao));

            Key keyWithZeros = new Key(bytesWithZeros);
            Assert.True(keyWithZeros.ToSpan().SequenceEqual(bytesWithZeros));
        }

        [Fact]
        public void InterningWorks()
        {
            Key keyFoo = new Key("foo");
            Key keyBar = new Key("bar");

            Key keyFoo2 = new Key(bytesFoo);
            Assert.Equal("foo", keyFoo2.ToString());

            // Self
            Assert.True(keyFoo.Equals(keyFoo));
            Assert.True(keyFoo.Equals((KeyRef)keyFoo));
            Assert.True(keyFoo.Equals((object)keyFoo));
            Assert.True(keyFoo.Equals(keyFoo.AsKeyRef()));

            // Similar
            Assert.True(keyFoo.Equals(keyFoo2));
            Assert.True(keyFoo.Equals((object)keyFoo2));
            Assert.True(keyFoo.Equals((KeyRef)keyFoo2));
            Assert.True(keyFoo.Equals(keyFoo2.AsKeyRef()));

            // Different
            Assert.False(keyFoo.Equals(keyBar));
            Assert.False(keyFoo.Equals((object)keyBar));
            Assert.False(keyFoo.Equals((KeyRef)keyBar));
            Assert.False(keyFoo.Equals(keyBar.AsKeyRef()));
        }

        [Fact]
        public void HashesAreExpected()
        {
            Key keyFoo = new Key("foo");
            Assert.Equal(0xCA796135, (uint)keyFoo.GetHashCode());
            Assert.Equal(0x836E8217CA796135ul, keyFoo.Hash);

            Key keyBar = new Key("bar");
            Assert.Equal(0x562585D9, keyBar.GetHashCode());
            Assert.Equal(0x6E84777F562585D9ul, keyBar.Hash);

            Key keyWithZeros = new Key(bytesWithZeros);
            Assert.Equal(0x6B4EE12F, keyWithZeros.GetHashCode());
            Assert.Equal(0x8F5FBB1D6B4EE12Ful, keyWithZeros.Hash);
        }
    }
}
