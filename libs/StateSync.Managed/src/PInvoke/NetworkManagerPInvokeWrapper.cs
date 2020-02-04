using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync.PInvoke
{
    /// <summary>
    /// Wraps a <see cref="NetworkManager"/> object for PInvoke purposes
    /// and maintains the cache of C++ wrappers for the channels created by it.
    /// </summary>
    internal class NetworkManagerWrapper
    {
        private readonly NetworkManager manager_;

        // A connection created on the C# side can be wrapped by a NetworkConnection object on C++ side,
        // that will hold a GCHandle to the C# NetworkConnection.
        //
        // To avoid the creation of the new wrapper every time the C++ side calls GetConnection,
        // we are maintaining the cache of already created wrappers, but to avoid cyclic references,
        // we only store a weak pointer to the wrapper.
        // The weak pointer is separately allocated on the C++ side, and the pointer to it is
        // stored as a handle inside CppNetworkConnectionWeakPtr, witch are values in connectionWrappersCache_
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
        //  std::weak_ptr<NetworkConnection> <--||-- handle <- C#CppNetworkConnectionWeakPtr
        //
        ConditionalWeakTable<NetworkConnection, CppNetworkConnectionWeakPtr> connectionWrappersCache_
            = new ConditionalWeakTable<NetworkConnection, CppNetworkConnectionWeakPtr>();

        /// <summary>
        /// An object that holds a handle to a std::weak_ptr of the NetworkConnection allocated on the C++ side.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal class CppNetworkConnectionWeakPtr : CriticalHandle
        {
            public CppNetworkConnectionWeakPtr() : base(IntPtr.Zero)
            {
                handle = CppNetworkConnectionWeakPtr_Create();
            }

            public override bool IsInvalid => handle != IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                CppNetworkConnectionWeakPtr_Destroy(handle);
                return true;
            }

            /// <summary>
            /// Initializes the shared_ptr on the C++ side (pointed by cppNetworkConnectionSharedPtr)
            /// with the shared_ptr to the ConnectionWrapper for the provided connection.
            /// This either acquires a reference to the existing wrapper,
            /// or creates a new wrapper if necessary.
            /// </summary>
            public unsafe void AcquireConnectionWrapper(
                NetworkConnection connection,
                void* cppNetworkManagerWrapper,
                IntPtr connectionStringBlob,
                void* cppNetworkConnectionSharedPtr)
            {
                if (!CppNetworkConnectionWeakPtr_Lock(handle, cppNetworkConnectionSharedPtr))
                {
                    CppNetworkConnectionWeakPtr_Update(
                        handle,
                        cppNetworkManagerWrapper,
                        connectionStringBlob,
                        GCHandle.ToIntPtr(GCHandle.Alloc(connection)),
                        cppNetworkConnectionSharedPtr);
                }
            }
        }

        internal struct ListenerWrapper : NetworkListener
        {
            public unsafe ListenerWrapper(void* cppListener) { this.cppListener = cppListener; }

            unsafe void* cppListener;

            public unsafe void OnMessage(InternedBlobRef senderConnectionString, ReadOnlySpan<byte> message)
            {
                fixed (byte* bytes = message)
                {
                    NetworkListener_OnMessage(cppListener, senderConnectionString.handle, bytes, message.Length);
                }
            }
        }

        internal NetworkManagerWrapper(NetworkManager manager)
        {
            manager_ = manager;
        }

        internal unsafe delegate void GetConnectionDelegate(
            IntPtr connectionStringBlob,
            IntPtr managerGCHandle,
            void* cppNetworkManagerWrapper,
            void* cppNetworkConnectionSharedPtr);


        internal unsafe delegate void PollMessageDelegate(
            IntPtr managerGCHandle,
            void* cppListener);

        public unsafe delegate void SendMessageDelegate(
            IntPtr connectionGCHandle,
            byte* messageBegin,
            int messageSize);

        /// <summary>
        /// Invokes <see cref="NetworkManager.GetConnection(InternedBlobRef)"/>
        /// and writes std::shared_ptr to the C++ wrapper object to the result to the location
        /// pointed by <paramref name="cppNetworkConnectionSharedPtr"/>. The wrapper is either obtained from
        /// the cache of wrappers, or created on demand.
        /// </summary>
        internal static unsafe void InvokeGetConnection(
            IntPtr connectionStringBlob,
            IntPtr managerGCHandle,
            void* cppNetworkManagerWrapper,
            void* cppNetworkConnectionSharedPtr)
        {
            var managerWrapper =
                GCHandle.FromIntPtr(managerGCHandle).Target as NetworkManagerWrapper;
            try
            {
                var connection = managerWrapper.manager_.GetConnection(
                    new InternedBlobRef(connectionStringBlob));
                lock (managerWrapper.connectionWrappersCache_)
                {
                    CppNetworkConnectionWeakPtr weakWrapper;
                    if (!managerWrapper.connectionWrappersCache_.TryGetValue(connection, out weakWrapper))
                    {
                        weakWrapper = new CppNetworkConnectionWeakPtr();
                        managerWrapper.connectionWrappersCache_.Add(connection, weakWrapper);
                    }
                    weakWrapper.AcquireConnectionWrapper(connection, cppNetworkManagerWrapper, connectionStringBlob, cppNetworkConnectionSharedPtr);
                }
            }
            catch
            {
                // the result pointed by cppNetworkConnectionSharedPtr on the caller's side will stay empty.
            }
        }

        /// <summary>
        /// Invokes <see cref="NetworkManager.PollMessage(NetworkListener)"/>
        /// on the provided handle to <see cref="NetworkManager"/>.
        /// </summary>
        /// <param name="managerGCHandle">
        ///     <see cref="GCHandle"/> to <see cref="NetworkManager"/> as <see cref="IntPtr"/>.
        /// </param>
        /// <param name="cppListener">
        ///     Pointer to the NetworkListener object on the C++ side.
        /// </param>
        internal static unsafe bool InvokePollMessage(
                IntPtr managerGCHandle,
                void* cppListener)
        {
            var manager =
                GCHandle.FromIntPtr(managerGCHandle).Target as NetworkManagerWrapper;
            ListenerWrapper listenerWrapper = new ListenerWrapper(cppListener);
            return manager.manager_.PollMessage(listenerWrapper);
        }

        /// <summary>
        /// Invokes <see cref="NetworkConnection.SendMessage(ReadOnlySpan{byte})"/>
        /// on the provided handle to <see cref="NetworkConnection"/>.
        /// </summary>
        /// <param name="networkConnectionGCHandle">
        ///     <see cref="GCHandle"/> to <see cref="NetworkConnection"/> as <see cref="IntPtr"/>.
        /// </param>
        internal static unsafe void InvokeSendMessage(
                IntPtr networkConnectionGCHandle,
                byte* messageBegin,
                int messageSize)
        {
            var networkConnection =
                GCHandle.FromIntPtr(networkConnectionGCHandle).Target as NetworkConnection;
            networkConnection.SendMessage(new ReadOnlySpan<byte>(messageBegin, messageSize));
        }

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Create")]
        private static extern IntPtr CppNetworkConnectionWeakPtr_Create();

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Destroy")]
        private static extern void CppNetworkConnectionWeakPtr_Destroy(IntPtr cppWeakPtrHandle);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Lock")]
        private static extern unsafe bool CppNetworkConnectionWeakPtr_Lock(
            IntPtr cppWeakPtrHandle,
            void* cppNetworkConnectionSharedPtr);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_CppNetworkConnectionWeakPtr_Update")]
        private static extern unsafe void CppNetworkConnectionWeakPtr_Update(
            IntPtr cppWeakPtrHandle,
            void* cppNetworkManagerWrapper,
            IntPtr connectionStringBlob,
            IntPtr connectonGCHandle,
            void* cppNetworkConnectionSharedPtr);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_NetworkListener_OnMessage")]
        private static extern unsafe void NetworkListener_OnMessage(
            void* cppListener,
            IntPtr senderInternedBlob,
            byte* messageBegin,
            int messageSize);
    }
}
