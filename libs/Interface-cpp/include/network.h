#pragma once

#include <cstdint>
#include "export.h"

struct mrsEndpoint;
struct mrsChannelCategory;
struct mrsChannelQueue;
struct mrsMessage;
struct mrsChannel;

// 0 = success, otherwise failure
using Result = int;

enum class mrsChannelType : unsigned char { kUnordered, kOrdered };

extern "C" {

MRS_API Result MRS_CALL mrsInit();
MRS_API Result MRS_CALL mrsQuit();

// IEndpoint

// Get a reference to an IEndpoint. Release with mrsReleaseEndpoint when
// finished.
MRS_API Result MRS_CALL mrsAcquireEndpoint(const char* id,
                                           uint32_t id_size,
                                           mrsEndpoint** endpoint_out);

// Destroy with mrsDisposeString.
MRS_API Result MRS_CALL mrsGetEndpointId(mrsEndpoint* endpoint,
                                         const char** id_out,
                                         uint32_t* id_size_out);

MRS_API Result MRS_CALL mrsReleaseEndpoint(mrsEndpoint*);

// IChannelCategory

// Destroy with mrsDisposeCategory.
MRS_API Result MRS_CALL mrsCreateCategory(const char* name,
                                          uint32_t name_size,
                                          mrsChannelType type,
                                          mrsChannelCategory** category_out);

// Destroy with mrsDisposeString.
MRS_API Result MRS_CALL mrsGetCategoryName(mrsChannelCategory* category,
                                           const char** id_out,
                                           uint32_t* id_size_out);

MRS_API Result MRS_CALL mrsGetCategoryType(mrsChannelCategory* category,
                                           mrsChannelType* type_out);

// Stays alive until the category dies. No need to dispose.
MRS_API Result MRS_CALL mrsGetCategoryQueue(mrsChannelCategory* category,
                                            mrsChannelQueue** queue_out);

MRS_API Result MRS_CALL mrsCategoryStartListening(mrsChannelCategory* category);

MRS_API Result MRS_CALL mrsCategoryStopListening(mrsChannelCategory* category);

MRS_API Result MRS_CALL mrsDisposeCategory(mrsChannelCategory* category);

// IMessage

MRS_API Result MRS_CALL
mrsGetMessageCategory(mrsMessage* message, mrsChannelCategory** category_out);

MRS_API Result MRS_CALL mrsGetMessageEndpoint(mrsMessage* message,
                                              mrsEndpoint* endpoint_out);

MRS_API Result MRS_CALL mrsGetMessagePayload(mrsMessage* message,
                                             const char** payload_out,
                                             uint32_t* payload_size_out);

MRS_API Result MRS_CALL mrsReleaseMessage(mrsMessage* message);

// IMessageQueue

MRS_API Result MRS_CALL mrsQueueTake(mrsChannelQueue* queue,
                                     mrsMessage** message_out);

MRS_API Result MRS_CALL mrsQueueTryTake(mrsChannelQueue* queue,
                                      mrsMessage** message_out);

// IChannel

// Destroy with mrsDisposeChannel.
MRS_API Result MRS_CALL mrsCreateChannel(mrsChannelCategory category,
                                         mrsEndpoint endpoint,
                                         mrsChannel** channel_out);

MRS_API Result MRS_CALL
mrsGetChannelCategory(mrsChannel* channel, mrsChannelCategory** category_out);

MRS_API Result MRS_CALL mrsGetChannelEndpoint(mrsChannel* channel,
                                              mrsEndpoint** endpoint_out);

MRS_API Result MRS_CALL mrsIsChannelOk(mrsChannel* channel, uint8_t* ok_out);

MRS_API Result MRS_CALL mrsGetChannelSendQueueCount(mrsChannel* channel, uint32_t* count_out);

MRS_API Result MRS_CALL mrsChannelReconnect(mrsChannel* channel);

MRS_API Result MRS_CALL mrsChannelSendMessage(mrsChannel* channel,
                                              const char* payload,
                                              uint32_t payload_size);

MRS_API Result MRS_CALL mrsDisposeChannel(mrsChannel channel);

// Dispose strings returned by methods above.
MRS_API Result MRS_CALL mrsDisposeString(uint32_t size, const char* ptr);
}
