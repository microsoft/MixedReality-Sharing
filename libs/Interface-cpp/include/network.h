#pragma once

#include "export.h"

typedef void* mrsEndpoint;
typedef void* mrsChannelCategory;
typedef void* mrsChannelQueue;
typedef void* mrsMessage;
typedef void* mrsChannel;

enum class mrsChannelType : unsigned char { UNORDERED, ORDERED };

extern "C" {

MRS_API void MRS_CALL mrsInit();
MRS_API void MRS_CALL mrsQuit();

// IEndpoint
MRS_API mrsEndpoint MRS_CALL mrsAcquireEndpoint(size_t id_size, const char* id);
MRS_API void MRS_CALL mrsGetEndpointId(mrsEndpoint endpoint,
                                       size_t* id_size_out,
                                       const char** id_out);
MRS_API mrsEndpoint MRS_CALL mrsReleaseEndpoint(mrsEndpoint);

// IChannelCategory
MRS_API mrsChannelCategory MRS_CALL mrsCreateCategory(size_t name_size,
                                                      const char* name,
                                                      mrsChannelType type);
MRS_API void MRS_CALL mrsGetCategoryName(mrsChannelCategory category,
                                         size_t* id_size_out,
                                         const char** id_out);
MRS_API mrsChannelType MRS_CALL mrsGetCategoryType(mrsChannelCategory category);
MRS_API mrsChannelQueue MRS_CALL
mrsGetCategoryQueue(mrsChannelCategory category);
MRS_API void MRS_CALL mrsCategoryStartListening(mrsChannelCategory category);
MRS_API void MRS_CALL mrsCategoryStopListening(mrsChannelCategory category);
MRS_API void MRS_CALL mrsDisposeCategory(mrsChannelCategory category);

// IMessage
MRS_API mrsChannelCategory MRS_CALL mrsGetMessageCategory(mrsMessage message);
MRS_API mrsEndpoint MRS_CALL mrsGetMessageEndpoint(mrsMessage message);
MRS_API void MRS_CALL mrsGetMessagePayload(mrsMessage message,
                                           size_t* payload_size_out,
                                           const char** payload_out);
MRS_API void MRS_CALL mrsReleaseMessage(mrsMessage message);

// IMessageQueue
MRS_API mrsMessage MRS_CALL mrsQueueTake(mrsChannelQueue queue);
MRS_API bool MRS_CALL mrsQueueTryTake(mrsChannelQueue queue,
                                      mrsMessage* messageOut);

// IChannel
MRS_API mrsChannel MRS_CALL mrsCreateChannel(mrsChannelCategory category,
                                             mrsEndpoint endpoint);
MRS_API mrsChannelCategory MRS_CALL mrsGetChannelCategory(mrsChannel channel);
MRS_API mrsEndpoint MRS_CALL mrsGetChannelEndpoint(mrsChannel channel);
MRS_API bool MRS_CALL mrsIsChannelOk(mrsChannel channel);
MRS_API int MRS_CALL mrsGetChannelSendQueueCount(mrsChannel channel);
MRS_API void MRS_CALL mrsChannelReconnect(mrsChannel channel);
MRS_API void MRS_CALL mrsChannelSendMessage(mrsChannel channel,
                                            size_t payload_size,
                                            const char* payload);
MRS_API void MRS_CALL mrsDestroyChannel(mrsChannel channel);

// Dispose strings returned by methods above.
MRS_API void MRS_CALL mrsDisposeString(size_t size, const char* ptr);
}
