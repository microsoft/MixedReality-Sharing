// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <Microsoft/MixedReality/Sharing/StateSync/RSMConnection.h>

#include <Microsoft/MixedReality/Sharing/Common/Guid.h>

namespace Microsoft::MixedReality::Sharing::StateSync {

class ReplicatedState : public VirtualRefCountedBase {
 public:
  ~ReplicatedState() noexcept override;

  static RefPtr<ReplicatedState> Create(
      Guid guid,
      RefPtr<RSMConnection> connection) noexcept;

  const Guid& guid() const noexcept { return guid_; }

 private:
  const Guid guid_;
  RefPtr<RSMConnection> connection_;

  ReplicatedState(Guid guid, RefPtr<RSMConnection> connection);
};

}  // namespace Microsoft::MixedReality::Sharing::StateSync
