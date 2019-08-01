// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Sharing.Utilities
{
    /// <summary>
    /// A simple logger interface to be passed as a dependency.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Log a informative message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void Log(string message);

        /// <summary>
        /// Log a warning message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Log an error message.
        /// </summary>
        /// <param name="message">Message to log.</param>
        /// <param name="exception">(Optional) Exception accompanying the message.</param>
        void LogError(string message, Exception exception = null);
    }
}
