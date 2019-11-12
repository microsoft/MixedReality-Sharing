// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#if defined(_MSC_VER)
#if defined(MS_MR_SHARING_STATESYNC_EXPORT)
#define MS_MR_SHARING_STATESYNC_API __declspec(dllexport)
#else
#define MS_MR_SHARING_STATESYNC_API __declspec(dllimport)
#endif
#define MS_MR_CALL __stdcall
#elif defined(__ANDROID__)
#define MS_MR_SHARING_STATESYNC_API __attribute__((visibility("default")))
#define MS_MR_CALL __attribute__((stdcall))
#endif
