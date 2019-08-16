using System;
using System.Diagnostics;

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

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="message">The message string to log.</param>
        public static void Log(string message)
        {
            Trace.WriteLine(message, VerboseCategory);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message string to log.</param>
        public static void LogWarning(string message)
        {
            Trace.WriteLine(message, WarningCategory);
        }

        /// <summary>
        /// Logs an error message, optionally logging the accompanying exception.
        /// </summary>
        /// <param name="message">The message string to log.</param>
        /// <param name="exception">Optional exception to be logged.</param>
        public static void LogError(string message, Exception exception = null)
        {
            Trace.WriteLine(message, ErrorCategory);
            if (exception != null)
            {
                Trace.WriteLine(exception, ErrorCategory);
            }
        }
    }
}
