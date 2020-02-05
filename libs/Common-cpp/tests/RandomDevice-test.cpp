// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "pch.h"

#include <Microsoft/MixedReality/Sharing/Common/RandomDevice.h>

using namespace Microsoft::MixedReality::Sharing;

namespace {

TEST(RandomDevice, advance_works) {
  RandomDevice rd{0x3cfe4d1177ecc6a5ull, 0xd5e7fe74b35a5d2cull,
                  0xb55681d95d037ef7ull, 0xfcc3a9b769225ea5ull};

  // The expected constants are obtained from the reference implementation.
  EXPECT_EQ(rd(), 0xa16ed4a41d09a7a0ull);
  EXPECT_EQ(rd(), 0x11766172eb1feacbull);
  EXPECT_EQ(rd(), 0xb324f37982583039ull);
  EXPECT_EQ(rd(), 0x280d9c96f8f9e35full);
  EXPECT_EQ(rd(), 0x0f8d8105d7c2b3a4ull);
  EXPECT_EQ(rd(), 0x984a552d6153014dull);
  EXPECT_EQ(rd(), 0xc7f101c25d732dacull);
  EXPECT_EQ(rd(), 0xffdc0542a2676ab3ull);
  EXPECT_EQ(rd(), 0xf1fd5de3737ee0e6ull);
  EXPECT_EQ(rd(), 0x34baadb7268196acull);
  EXPECT_EQ(rd(), 0xb51a9b3f94ba24d9ull);
  EXPECT_EQ(rd(), 0xe587b3c288348b84ull);
  EXPECT_EQ(rd(), 0xa44a9f93d1c5626cull);
  EXPECT_EQ(rd(), 0x94328f6d9bdc335eull);
  EXPECT_EQ(rd(), 0x220fac91dd114a4full);
  EXPECT_EQ(rd(), 0x703a23fcdc5457a0ull);
  EXPECT_EQ(rd(), 0xccc13a8fc0ad846aull);
  EXPECT_EQ(rd(), 0x56c6c00477e185c5ull);
  EXPECT_EQ(rd(), 0x177836d90d0bed2full);
  EXPECT_EQ(rd(), 0x10a87b2d143e0a53ull);
  EXPECT_EQ(rd(), 0x087a665c1703938cull);
  EXPECT_EQ(rd(), 0xb937504c78e072bfull);
  EXPECT_EQ(rd(), 0xf013a07f51e84659ull);
  EXPECT_EQ(rd(), 0xca07032bd76f1c5eull);
  EXPECT_EQ(rd(), 0x12f866c96e9c1643ull);
  EXPECT_EQ(rd(), 0x1a64385b18262d73ull);
  EXPECT_EQ(rd(), 0x38469fb72d21b5efull);
  EXPECT_EQ(rd(), 0x1271130fc75a8988ull);
  EXPECT_EQ(rd(), 0xdc7a8a74ffa13b8bull);
  EXPECT_EQ(rd(), 0x2f95f9a759b4f35full);
  EXPECT_EQ(rd(), 0x0516b0d8ffdba965ull);
  EXPECT_EQ(rd(), 0xb416309cf3c760faull);
}

TEST(RandomDevice, jump_works) {
  RandomDevice rd{0x3cfe4d1177ecc6a5ull, 0xd5e7fe74b35a5d2cull,
                  0xb55681d95d037ef7ull, 0xfcc3a9b769225ea5ull};

  auto jump_next = [&] {
    rd.JumpForTestingPurposesOnly();
    return rd();
  };

  // The expected constants are obtained from the reference implementation.
  EXPECT_EQ(jump_next(), 0x364e910d3d17e57full);
  EXPECT_EQ(jump_next(), 0x9f4c6c5f46027606ull);
  EXPECT_EQ(jump_next(), 0x1b34af212944db8aull);
  EXPECT_EQ(jump_next(), 0xbd76eb2e9f3f86d0ull);
  EXPECT_EQ(jump_next(), 0x1d30af3161cc2107ull);
  EXPECT_EQ(jump_next(), 0x522a23d31ad2ed66ull);
  EXPECT_EQ(jump_next(), 0xb34cf669af0ec455ull);
  EXPECT_EQ(jump_next(), 0x0176a64c8cafe394ull);
  EXPECT_EQ(jump_next(), 0xca1dc2655b44a62aull);
  EXPECT_EQ(jump_next(), 0xca77ee224cf2e6e3ull);
  EXPECT_EQ(jump_next(), 0x7605983eb88a13a8ull);
  EXPECT_EQ(jump_next(), 0xf47b992fbc839e59ull);
  EXPECT_EQ(jump_next(), 0x0a6393bf1a2fc8cfull);
  EXPECT_EQ(jump_next(), 0xd829a62ac3ef7940ull);
  EXPECT_EQ(jump_next(), 0x174c92a2a7ea89ecull);
  EXPECT_EQ(jump_next(), 0xe313f565ab527e05ull);
  EXPECT_EQ(jump_next(), 0xcaeaa50e2ccb8722ull);
  EXPECT_EQ(jump_next(), 0x4af60a76ef49fa98ull);
  EXPECT_EQ(jump_next(), 0x497420f13cf297f2ull);
  EXPECT_EQ(jump_next(), 0x90a056f55eb4ebfbull);
  EXPECT_EQ(jump_next(), 0x4135b79eecf3c4baull);
  EXPECT_EQ(jump_next(), 0x35b79c76d2d40762ull);
  EXPECT_EQ(jump_next(), 0x65b241280c23b1e1ull);
  EXPECT_EQ(jump_next(), 0x1faea154eb46d66bull);
  EXPECT_EQ(jump_next(), 0x9e29e266a3dac1bfull);
  EXPECT_EQ(jump_next(), 0x5d7444cbadab142dull);
  EXPECT_EQ(jump_next(), 0x6568343efca2786full);
  EXPECT_EQ(jump_next(), 0x2cd357dc1934253cull);
  EXPECT_EQ(jump_next(), 0x917a5a7747ee7f16ull);
  EXPECT_EQ(jump_next(), 0x23a0c8aea55eb4a0ull);
  EXPECT_EQ(jump_next(), 0xec2a9d3c01f35a59ull);
  EXPECT_EQ(jump_next(), 0x78406efb089be6eaull);
}

}  // namespace
