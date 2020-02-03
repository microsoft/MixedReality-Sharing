using System;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface RSMConnection
    {
        /// <summary>
        /// Attempts to persist the command in the log of the RSM.
        /// </summary>
        /// <param name="command">The data of the command to be persisted</param>
        /// <returns>The unique identifier of the command.</returns>
        Guid SendCommand(ReadOnlySpan<byte> command);

        bool ProcessSingleUpdate(RSMListener listener);
    }

    // Temporary here
    public static class RSMUtil
    {
        public static RSMConnection CreateSingleServerRSM(
            ReadOnlySpan<byte> name,
            NetworkManager network_manager)
        {
            throw new NotImplementedException();
        }

        public static RSMConnection ConnectToSingleServerRSM(
          ReadOnlySpan<byte> name,
          NetworkManager network_manager,
          ReadOnlySpan<byte> connectionString)
        {
            throw new NotImplementedException();
        }
    }
}
