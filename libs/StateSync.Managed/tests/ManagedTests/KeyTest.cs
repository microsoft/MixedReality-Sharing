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
            InternedBlob keyFoo = new InternedBlob("foo");
            Assert.Equal("foo", keyFoo.ToString());
            Assert.True(keyFoo.ToSpan().SequenceEqual(bytesFoo));

            InternedBlob keyNihao = new InternedBlob("你好");
            Assert.Equal("你好", keyNihao.ToString());
            Assert.True(keyNihao.ToSpan().SequenceEqual(bytesNihao));

            InternedBlob keyWithZeros = new InternedBlob(bytesWithZeros);
            Assert.True(keyWithZeros.ToSpan().SequenceEqual(bytesWithZeros));
        }

        [Fact]
        public void InterningWorks()
        {
            InternedBlob keyFoo = new InternedBlob("foo");
            InternedBlob keyBar = new InternedBlob("bar");

            InternedBlob keyFoo2 = new InternedBlob(bytesFoo);
            Assert.Equal("foo", keyFoo2.ToString());

            // Self
            Assert.True(keyFoo.Equals(keyFoo));
            Assert.True(keyFoo.Equals((InternedBlobRef)keyFoo));
            Assert.True(keyFoo.Equals((object)keyFoo));
            Assert.True(keyFoo.Equals(keyFoo.AsBlobRef()));

            // Similar
            Assert.True(keyFoo.Equals(keyFoo2));
            Assert.True(keyFoo.Equals((object)keyFoo2));
            Assert.True(keyFoo.Equals((InternedBlobRef)keyFoo2));
            Assert.True(keyFoo.Equals(keyFoo2.AsBlobRef()));

            // Different
            Assert.False(keyFoo.Equals(keyBar));
            Assert.False(keyFoo.Equals((object)keyBar));
            Assert.False(keyFoo.Equals((InternedBlobRef)keyBar));
            Assert.False(keyFoo.Equals(keyBar.AsBlobRef()));
        }

        [Fact]
        public void HashesAreExpected()
        {
            InternedBlob keyFoo = new InternedBlob("foo");
            Assert.Equal(0xCA796135, (uint)keyFoo.GetHashCode());
            Assert.Equal(0x836E8217CA796135ul, keyFoo.Hash);

            InternedBlob keyBar = new InternedBlob("bar");
            Assert.Equal(0x562585D9, keyBar.GetHashCode());
            Assert.Equal(0x6E84777F562585D9ul, keyBar.Hash);

            InternedBlob keyWithZeros = new InternedBlob(bytesWithZeros);
            Assert.Equal(0x6B4EE12F, keyWithZeros.GetHashCode());
            Assert.Equal(0x8F5FBB1D6B4EE12Ful, keyWithZeros.Hash);
        }
    }
}
