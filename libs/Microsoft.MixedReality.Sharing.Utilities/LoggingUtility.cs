using System;
using System.Diagnostics;

namespace Microsoft.MixedReality.Sharing.Utilities
{
    /// <summary>
    /// Helper utility to standardize logging through Trace and categories.
    /// </summary>
    public class LoggingUtility : ILogger
    {
        public const string VerboseCategory = "Verbose";
        public const string WarningCategory = "Warning";
        public const string ErrorCategory = "Error";

        public static ILogger Logger { get; } = new LoggingUtility();

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

        private LoggingUtility() { }

        void ILogger.Log(string message)
        {
            Log(message);
        }

        void ILogger.LogWarning(string message)
        {
            LogWarning(message);
        }

        void ILogger.LogError(string message, Exception exception)
        {
            LogError(message, exception);
        }
    }
}
