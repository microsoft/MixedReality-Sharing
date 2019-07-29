using System;

namespace Microsoft.MixedReality.Sharing.Utilities
{
    public interface ILogger
    {
        void Log(string message);

        void LogWarning(string message);

        void LogError(string message, Exception exception = null);
    }
}
