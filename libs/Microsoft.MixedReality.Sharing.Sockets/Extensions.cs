// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Net.Sockets;

namespace Microsoft.MixedReality.Sharing.Sockets
{
    public static class Extensions
    {
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

        public static bool IsIPv4(this IPAddress ipAddress)
        {
            return ipAddress.AddressFamily == AddressFamily.InterNetwork;
        }

        internal static ushort GetUInt16LittleEndian(this byte[] buffer, int offset)
        {
            return (ushort)((buffer[1 + offset] << 8) ^ buffer[offset]);
        }

        internal static void SetAsUInt16LittleIndian(this byte[] buffer, ushort value, int offset)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }
    }
}
