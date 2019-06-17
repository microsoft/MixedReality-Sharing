#include "pch.h"
#include "../src/TempLib.h"

TEST(TestCaseName, TestName) {
  EXPECT_EQ(1, 1);
  EXPECT_TRUE(true);
  EXPECT_EQ(11, TempLib_doit());
}