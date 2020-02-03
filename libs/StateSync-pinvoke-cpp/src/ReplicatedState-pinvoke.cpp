// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#include "src/pch.h"

#include <Microsoft/MixedReality/Sharing/StateSync/NetworkConnection.h>
#include <Microsoft/MixedReality/Sharing/StateSync/NetworkListener.h>
#include <Microsoft/MixedReality/Sharing/StateSync/NetworkManager.h>
#include <Microsoft/MixedReality/Sharing/StateSync/ReplicatedState.h>

#include <Microsoft/MixedReality/Sharing/StateSync/export.h>

#include <Microsoft/MixedReality/Sharing/Common/bit_cast.h>

using namespace Microsoft::MixedReality::Sharing;
using namespace Microsoft::MixedReality::Sharing::StateSync;

namespace {

class PInvokeNetworkManager;

using ReleaseGCHandleDelegate = void(__stdcall*)(intptr_t gc_handle);

using GetConnectionDelegate =
    void(__stdcall*)(intptr_t connection_string_blob,
                     intptr_t manager_gc_handle,
                     PInvokeNetworkManager* pinvoke_network_manager,
                     std::shared_ptr<NetworkConnection>* result_location);

using PollMessageDelegate = bool(__stdcall*)(intptr_t manager_gc_handle,
                                             void* listener);

using SendMessageDelegate = intptr_t(__stdcall*)(intptr_t connection_gc_handle,
                                                 const char* message_begin,
                                                 int message_size);

class PInvokeNetworkManager : public NetworkManager {
 public:
  PInvokeNetworkManager(intptr_t manager_gc_handle,
                        ReleaseGCHandleDelegate release_gc_handle_delegate,
                        GetConnectionDelegate get_connection_delegate,
                        PollMessageDelegate poll_message_delegate,
                        SendMessageDelegate send_message_delegate)
      : manager_gc_handle_{manager_gc_handle},
        release_gc_handle_delegate_{release_gc_handle_delegate},
        get_connection_delegate_{get_connection_delegate},
        poll_message_delegate_{poll_message_delegate},
        send_message_delegate_{send_message_delegate} {}

  ~PInvokeNetworkManager() noexcept override {
    release_gc_handle_delegate_(manager_gc_handle_);
  }

  std::shared_ptr<NetworkConnection> GetConnection(
      const InternedBlob& connection_string) override;

  bool PollMessage(NetworkListener& listener) override {
    return poll_message_delegate_(manager_gc_handle_, &listener);
  }

  std::shared_ptr<NetworkConnection> CreateConnectionWrapper(
      const InternedBlob& connection_string,
      intptr_t connection_handle) {
    return std::make_shared<Connection>(&connection_string, this,
                                        connection_handle);
  }

 private:
  class Connection : public NetworkConnection {
   public:
    Connection(RefPtr<const InternedBlob> connection_string,
               RefPtr<PInvokeNetworkManager> manager,
               intptr_t connection_handle);
    ~Connection() noexcept override;
    void SendMessage(std::string_view message) override;

   private:
    RefPtr<PInvokeNetworkManager> manager_;
    intptr_t connection_handle_;
  };

  intptr_t manager_gc_handle_;
  ReleaseGCHandleDelegate release_gc_handle_delegate_;
  GetConnectionDelegate get_connection_delegate_;
  PollMessageDelegate poll_message_delegate_;
  SendMessageDelegate send_message_delegate_;
};

std::shared_ptr<NetworkConnection> PInvokeNetworkManager::GetConnection(
    const InternedBlob& connection_string) {
  std::shared_ptr<NetworkConnection> result;
  get_connection_delegate_(bit_cast<intptr_t>(&connection_string),
                           manager_gc_handle_, this, &result);
  return result;
}

PInvokeNetworkManager::Connection::Connection(
    RefPtr<const InternedBlob> connection_string,
    RefPtr<PInvokeNetworkManager> manager,
    intptr_t connection_handle)
    : NetworkConnection{std::move(connection_string)},
      manager_{std::move(manager)},
      connection_handle_{connection_handle} {}

PInvokeNetworkManager::Connection::~Connection() noexcept {
  manager_->release_gc_handle_delegate_(connection_handle_);
}

void PInvokeNetworkManager::Connection::SendMessage(std::string_view message) {
  if (message.size() > std::numeric_limits<int>::max()) {
    throw std::invalid_argument{
        "The message is too large to be marshaled to C#"};
  }
  manager_->send_message_delegate_(connection_handle_, message.data(),
                                   static_cast<int>(message.size()));
}

}  // namespace

extern "C" {

MS_MR_SHARING_STATESYNC_API intptr_t MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Create() noexcept {
  return bit_cast<intptr_t>(new std::weak_ptr<NetworkConnection>);
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Destroy(
    intptr_t weak_ptr_handle) noexcept {
  auto* wptr = bit_cast<std::weak_ptr<NetworkConnection>*>(weak_ptr_handle);
  delete wptr;
}

MS_MR_SHARING_STATESYNC_API bool MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Lock(
    intptr_t weak_ptr_handle,
    std::shared_ptr<NetworkConnection>* result) noexcept {
  auto* wptr = bit_cast<std::weak_ptr<NetworkConnection>*>(weak_ptr_handle);
  *result = wptr->lock();
  return result;
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Update(
    intptr_t weak_ptr_handle,
    PInvokeNetworkManager* manager,
    intptr_t connection_string_blob,
    intptr_t connecton_gc_handle,
    std::shared_ptr<NetworkConnection>* result) noexcept {
  *result = manager->CreateConnectionWrapper(
      *bit_cast<const InternedBlob*>(connection_string_blob),
      connecton_gc_handle);
  auto* wptr = bit_cast<std::weak_ptr<NetworkConnection>*>(weak_ptr_handle);
  *wptr = *result;  // Updating the cache.
}

MS_MR_SHARING_STATESYNC_API void MS_MR_CALL
Microsoft_MixedReality_Sharing_StateSync_NetworkListener_OnMessage(
    NetworkListener* listener,
    intptr_t sender,
    const char* messageBegin,
    int messageSize) noexcept {
  std::string_view message{messageBegin, static_cast<size_t>(messageSize)};
  listener->OnMessage(*bit_cast<const InternedBlob*>(sender), message);
}

}  // extern "C"
