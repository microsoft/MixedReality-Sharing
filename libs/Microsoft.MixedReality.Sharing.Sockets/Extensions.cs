// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public static class Extensions
    {
        /// <summary>
        /// Checks whether this address is a valid multicast address.
        /// </summary>
        /// <param name="ipAddress">The address to check.</param>
        /// <returns>True if the address is a multicast address.</returns>
        public static bool IsValidMulticastAddress(this IPAddress ipAddress)
        {
            if (ipAddress.IsIPv6Multicast)
            {
                return true;
            }
            else if (!ipAddress.IsIPv4())
            {
                return false;
            }

            // The address must be in the range [224.0.0.1, 239.255.255.255]
            byte[] bytes = ipAddress.GetAddressBytes();
            return bytes[0] >= 224 && bytes[0] <= 239;
        }

        /// <summary>
        /// Gets whether this <see cref="IPAddress"/> is an IPv4 address.
        /// </summary>
        /// <param name="ipAddress">The address to check.</param>
        /// <returns>True if the address is an IPv4 address.</returns>
        public static bool IsIPv4(this IPAddress ipAddress)
        {
            return ipAddress.AddressFamily == AddressFamily.InterNetwork;
        }

        /// <summary>
        /// Prevents abortion exceptions from trickling up, and gracefully exists the task.
        /// </summary>
        /// <param name="task">The task to ignore abortions for./param>
        /// <returns>A wrapping task for the given task.</returns>
        public static Task IgnoreSocketAbort(this Task task)
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // This will rethrow any remaining exceptions, if any.
                    t.Exception.Handle(ex => ex is OperationCanceledException || ex is ObjectDisposedException || (ex is SocketException socketException && socketException.SocketErrorCode == SocketError.OperationAborted));
                } // else do nothing
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// Prevents abortion exceptions from trickling up, and gracefully exists the task.
        /// </summary>
        /// <typeparam name="T">The result type of the Task.</typeparam>
        /// <param name="task">The task to ignore abortions for./param>
        /// <param name="defaultAbortResult">The default value to return in case the socket operation is aborted.</param>
        /// <returns>A wrapping task for the given task.</returns>
        public static Task<T> IgnoreSocketAbort<T>(this Task<T> task, T defaultAbortResult = default(T))
        {
            return task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    // This will rethrow any remaining exceptions, if any.
                    t.Exception.Handle(ex => ex is OperationCanceledException || ex is ObjectDisposedException || (ex is SocketException socketException && socketException.SocketErrorCode == SocketError.OperationAborted));
                    return defaultAbortResult;
                }

                return t.IsCanceled ? defaultAbortResult : t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// Asynchronously reads from a stream to transmit data, keeping only one size of the send buffer in memory at a time.
        /// </summary>
        /// <param name="socket">The socket to send on.</param>
        /// <param name="stream">The stream of data to transmit.</param>
        /// <returns>An awaitable task.</returns>
        public static async Task SendDataAsync(this Socket socket, Stream stream)
        {
            byte[] buffer = new byte[socket.SendBufferSize];
            while (stream.CanRead)
            {
                int size = await stream.ReadAsync(buffer, 0, buffer.Length);
                int numSent = await socket.SendAsync(new ArraySegment<byte>(buffer, 0, size), SocketFlags.None);

                if (size != numSent)
                {
                    // Did we fail here?
                }
            }
        }
    }
}
