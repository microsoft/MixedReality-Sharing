// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
#if NETFX_CORE
using Windows.Foundation.Diagnostics;
#endif

namespace Microsoft.MixedReality.Sharing.Utilities
{
    /// <summary>
    /// Helper utility to standardize logging through Trace and categories.
    /// </summary>
    public static class LoggingUtility
    {
        public const string VerboseCategory = "Verbose";
        public const string WarningCategory = "Warning";
        public const string ErrorCategory = "Error";
#if NETFX_CORE
        private static LoggingChannel _channel = new LoggingChannel("Microsoft.MixedReality.Sharing_Log", new LoggingChannelOptions());
#endif

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="message">The message string to log.</param>
        public static void Log(string message)
        {
#if !NETFX_CORE
            Trace.WriteLine(message, VerboseCategory);
#else
            _channel.LogMessage(message);
#endif
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message string to log.</param>
        public static void LogWarning(string message)
        {
#if !NETFX_CORE
            Trace.WriteLine(message, WarningCategory);
#else
            _channel.LogMessage(message, LoggingLevel.Warning);
#endif
        }

        /// <summary>
        /// Logs an error message, optionally logging the accompanying exception.
        /// </summary>
        /// <param name="message">The message string to log.</param>
        /// <param name="exception">Optional exception to be logged.</param>
        public static void LogError(string message, Exception exception = null)
        {
#if !NETFX_CORE
            Trace.WriteLine(message, ErrorCategory);
            if (exception != null)
            {
                Trace.WriteLine(exception, ErrorCategory);
            }
#else
            _channel.LogMessage(message, LoggingLevel.Error);
            if (exception != null)
            {
                _channel.LogMessage(exception.ToString(), LoggingLevel.Error);
            }
#endif
        }
    }
}
