// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
#if NETFX_CORE
using Windows.Foundation.Diagnostics;
#else
using System.Diagnostics;
#endif

namespace Microsoft.MixedReality.Sharing.Matchmaking
{
    internal static class MMLog
    {
        internal static void LogError(Exception exception, string message)
        {
#if NETFX_CORE
            _channel.LogMessage(message, LoggingLevel.Error);
            if (exception != null)
            {
                _channel.LogMessage(exception.ToString(), LoggingLevel.Error);
            }
#else
            Trace.TraceError(message);
            if (exception != null)
            {
                Trace.TraceError(exception.ToString());
            }
#endif
        }

        internal static void LogWarning(string fmt, params object[] args)
        {
#if NETFX_CORE
            _channel.LogMessage(String.Format(fmt, args), LoggingLevel.Warning);
#else
            Trace.TraceWarning(fmt, args);
#endif
        }

#if NETFX_CORE
        private static LoggingChannel _channel = new LoggingChannel("Microsoft.MixedReality.Sharing_Log", new LoggingChannelOptions());
#endif
    }
}