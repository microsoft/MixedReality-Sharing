// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once
#include <Microsoft/MixedReality/Sharing/StateSync/CommandId.h>

#include <Microsoft/MixedReality/Sharing/Common/VirtualRefCountedBase.h>

namespace Microsoft::MixedReality::Sharing::StateSync {

class RSMListener;

class RSMConnection : public VirtualRefCountedBase {
 public:
  // Attempts to persist the command in the log of the RSM.
  virtual CommandId SendCommand() = 0;

  // Processes a single incoming event of the RSM.
  // Returns true if there was an incoming event, false otherwise.
  virtual bool ProcessSingleUpdate(RSMListener& listener) = 0;
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
