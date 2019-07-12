#pragma once

// P/Invoke uses stdcall by default. This can be changed, but Unity's IL2CPP
// does not understand the CallingConvention attribute and instead
// unconditionally forces stdcall. So use stdcall in the API to be compatible.
#if defined(_MSC_VER)
#if defined(MR_SHARING_EXPORT)
#define MRS_API __declspec(dllexport)
#else
#define MRS_API __declspec(dllimport)
#endif
#define MRS_CALL __stdcall
#elif defined(__ANDROID__)
#define MRS_API __attribute__((visibility("default")))
#define MRS_CALL __attribute__((stdcall))
#endif
