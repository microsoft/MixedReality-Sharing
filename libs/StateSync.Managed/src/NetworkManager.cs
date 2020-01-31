// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    public interface NetworkManager
    {
        NetworkConnection GetConnection(InternedBlobRef connectionString);

        bool PollMessage(NetworkListener listener);
    }

    /// <summary>
    /// Maintains the cache of C++ wrappers for the created channels.
    /// </summary>
    public class NetworkManagerPInvokeWrapper
    {
        private readonly NetworkManager manager_;

        // A connection created on the C# side can be wrapped by a NetworkConnection object on C++ side,
        // that will hold a GCHandle to the C# NetworkConnection.
        //
        // To avoid the creation of the new wrapper every time the C++ side calls GetConnection,
        // we are maintaining the cache of already created wrappers, but to avoid cyclic references,
        // we only store a weak pointer to the wrapper.
        // The weak pointer is separately allocated on the C++ side, and the pointer to it is
        // stored as a handle inside NetworkConnectionWeakPtr, witch are values in connectionWrappersCache_
        //
        // The ownership model looks like like this:
        //                              (language boundary)
        //                                      ||
        //  C++NetworkConnection -> GCHandle ---||--------> C#Connection
        //    ^                                 ||            ^
        //    :                                 ||            : (weak dictionary key, see the documentation for ConditionalWeakTable)
        //    :                                 ||            :
        //    : (weak)                          ||            connectionWrappersCache_
        //    :                                 ||                                  |
        //    :                                 ||  (value in ConditionalWeakTable) |
        //    :                                 ||                                  v
        //  std::weak_ptr<NetworkConnection> <--||-- handle <- C#NetworkConnectionWeakPtr
        //
        ConditionalWeakTable<NetworkConnection, NetworkConnectionWeakPtr> connectionWrappersCache_
            = new ConditionalWeakTable<NetworkConnection, NetworkConnectionWeakPtr>();

        /// <summary>
        /// An object that holds a handle to a std::weak_ptr of the NetworkConnection allocated on the C++ side.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class NetworkConnectionWeakPtr : CriticalHandle
        {
            public NetworkConnectionWeakPtr() : base(IntPtr.Zero)
            {
                handle = NetworkConnectionWeakPtr_Create();
            }

            public override bool IsInvalid => handle != IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                NetworkConnectionWeakPtr_Destroy(handle);
                return true;
            }

            public unsafe void InitConnectionWrapper(
                NetworkConnection connection,
                void* networkManagerWrapper,
                IntPtr connectionString,
                void* resultLocation)
            {
                if (!NetworkConnectionWeakPtr_Lock(handle, resultLocation))
                {
                    NetworkConnectionWeakPtr_Update(
                        handle,
                        networkManagerWrapper,
                        connectionString,
                        GCHandle.ToIntPtr(GCHandle.Alloc(connection)),
                        resultLocation);
                }
            }
        }

        public struct ListenerWrapper : NetworkListener
        {
            public unsafe ListenerWrapper(void* listener) { this.listener = listener; }

            unsafe void* listener;

            public unsafe void OnMessage(InternedBlobRef senderConnectionString, ReadOnlySpan<byte> message)
            {
                fixed (byte* bytes = message)
                {
                    NetworkListener_OnMessage(listener, senderConnectionString.handle, bytes, message.Length);
                }
            }
        }

        public NetworkManagerPInvokeWrapper(NetworkManager manager)
        {
            manager_ = manager;
        }

        public unsafe delegate void GetConnectionDelegate(
            IntPtr connectionString,
            IntPtr managerGCHandle,
            void* networkManagerWrapper,
            void* resultLocation);


        public unsafe delegate void PollMessageDelegate(
            IntPtr managerGCHandle,
            void* listener);

        public unsafe delegate void SendMessageDelegate(
            IntPtr connectionGCHandle,
            byte* messageBegin,
            int messageSize);

        /// <summary>
        /// Invokes <see cref="NetworkManager.GetConnection(InternedBlobRef)"/>
        /// and writes std::shared_ptr to the C++ wrapper object to the result to the location
        /// pointed by <paramref name="resultLocation"/>. The wrapper is either obtained from
        /// the cache of wrappers, or created on demand.
        /// </summary>
        public static unsafe void InvokeGetConnection(
            IntPtr connectionString,
            IntPtr managerGCHandle,
            void* networkManagerWrapper,
            void* resultLocation)
        {
            var managerWrapper =
                GCHandle.FromIntPtr(managerGCHandle).Target as NetworkManagerPInvokeWrapper;
            try
            {
                var connection = managerWrapper.manager_.GetConnection(
                    new InternedBlobRef(connectionString));
                lock (managerWrapper.connectionWrappersCache_)
                {
                    NetworkConnectionWeakPtr weakWrapper;
                    if (!managerWrapper.connectionWrappersCache_.TryGetValue(connection, out weakWrapper))
                    {
                        weakWrapper = new NetworkConnectionWeakPtr();
                        managerWrapper.connectionWrappersCache_.Add(connection, weakWrapper);
                    }
                    weakWrapper.InitConnectionWrapper(connection, networkManagerWrapper, connectionString, resultLocation);
                }
            }
            catch
            {
                // the result pointed by resultLocation on the caller's side will stay empty.
            }
        }

        /// <summary>
        /// Invokes <see cref="NetworkConnection.SendMessage(ReadOnlySpan{byte})"/>
        /// on the provided handle to <see cref="NetworkConnection"/>.
        /// </summary>
        /// <param name="networkConnectionHandle">
        ///     <see cref="GCHandle"/> to <see cref="NetworkConnection"/> as <see cref="IntPtr"/>.
        /// </param>
        public static unsafe bool InvokePollMessage(
                IntPtr managerGCHandle,
                void* listener)
        {
            var manager =
                GCHandle.FromIntPtr(managerGCHandle).Target as NetworkManagerPInvokeWrapper;
            ListenerWrapper listenerWrapper = new ListenerWrapper(listener);
            return manager.manager_.PollMessage(listenerWrapper);
        }

        /// <summary>
        /// Invokes <see cref="NetworkConnection.SendMessage(ReadOnlySpan{byte})"/>
        /// on the provided handle to <see cref="NetworkConnection"/>.
        /// </summary>
        /// <param name="networkConnectionHandle">
        ///     <see cref="GCHandle"/> to <see cref="NetworkConnection"/> as <see cref="IntPtr"/>.
        /// </param>
        public static unsafe void InvokeSendMessage(
                IntPtr networkConnectionHandle,
                byte* messageBegin,
                int messageSize)
        {
            var networkConnection =
                GCHandle.FromIntPtr(networkConnectionHandle).Target as NetworkConnection;
            networkConnection.SendMessage(new ReadOnlySpan<byte>(messageBegin, messageSize));
        }

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_NetworkConnectionWeakPtr_Create")]
        private static extern IntPtr NetworkConnectionWeakPtr_Create();

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_NetworkConnectionWeakPtr_Destroy")]
        private static extern void NetworkConnectionWeakPtr_Destroy(IntPtr weakPtrHandle);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_NetworkConnectionWeakPtr_Lock")]
        private static extern unsafe bool NetworkConnectionWeakPtr_Lock(
            IntPtr weakPtrHandle,
            void* sharedPtrResult);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_NetworkConnectionWeakPtr_Update")]
        private static extern unsafe void NetworkConnectionWeakPtr_Update(
            IntPtr weakPtrHandle,
            void* networkManagerWrapper,
            IntPtr connectionString,
            IntPtr connectonGCHandle,
            void* sharedPtrResult);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_NetworkListener_OnMessage")]
        private static extern unsafe void NetworkListener_OnMessage(
            void* listener,
            IntPtr sender,
            byte* messageBegin,
            int messageSize);
    }
}
