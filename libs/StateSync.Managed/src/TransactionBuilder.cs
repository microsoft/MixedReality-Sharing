// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Sharing.StateSync
{
    /// <summary>
    /// A builder for <see cref="Transaction"/> objects.
    /// TODO: trivial transactions should be constructible directly.
    /// The builder should only be used for complex transactions.
    /// </summary>
    public class TransactionBuilder : Utilities.VirtualRefCountedBase
    {
        public TransactionBuilder()
        {
            handle = PInvoke_Create();
        }

        /// <summary>
        /// Creates an immutable transaction that contains actions
        /// and requirements requested from this builder.
        /// </summary>
        /// TODO: figure out if the builder should be reusable after this.
        public Transaction CreateTransaction()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Instructs the transaction to write the value to the subkey of the key.
        /// </summary>
        /// <remarks>The effect of any previous Put() or Delete() calls on the same subkey
        /// within this transaction will be overwritten by this call.
        /// When the transaction is applied, the new value will overwrite any previous
        /// value that this subkey had (or insert it if it was missing).</remarks>
        public void Put(InternedBlobRef key, ulong subkey, ValueRef value)
        {
            PInvoke_Put(handle, key.handle, subkey, value.handle);
        }

        /// <summary>
        /// Instructs the transaction to write the value to the subkey of the key.
        /// </summary>
        /// <remarks>The effect of any previous Put() or Delete() calls on the same subkey
        /// within this transaction will be overwritten by this call.
        /// When the transaction is applied, the new value will overwrite any previous
        /// value that this subkey had (or insert it if it was missing).</remarks>
        public unsafe void Put(InternedBlobRef key, ulong subkey, ReadOnlySpan<byte> value)
        {
            fixed (byte* bytes = value)
            {
                PInvoke_PutBytes(handle, key.handle, subkey, bytes, value.Length);
            }
        }

        /// <summary>
        /// Instructs the transaction to delete the subkey of the key.
        /// </summary>
        /// <remarks>The effect of any previous Put() or Delete() calls on the same subkey
        /// within this transaction will be overwritten by this call.</remarks>
        public void Delete(InternedBlobRef key, ulong subkey)
        {
            PInvoke_DeleteSubkey(handle, key.handle, subkey);
        }

        /// <summary>
        /// Instructs the transaction to delete all subkeys of the key.
        /// </summary>
        /// <remarks>The effect of any previous Put() or Delete() calls on the same key
        /// within this transaction will be overwritten by this call.
        /// 
        /// Note that this marks the whole key for deletion, and the exact number
        /// of affected subkeys is only known only when the transaction is applied.
        /// 
        /// Use it to safely delete the key instead of calling <see cref="Delete(InternedBlobRef, ulong)"/>
        /// on individual subkeys when this is the intended effect.
        /// 
        /// You can call <see cref="Put"/> for the same key after calling <see cref="Delete(InternedBlobRef)"/>
        /// if you want to insert any subkeys within the same transaction.</remarks>
        public void Delete(InternedBlobRef key)
        {
            PInvoke_DeleteKey(handle, key.handle);
        }

        /// <summary>
        /// Instructs the transaction to check that the subkey is present before
        /// applying the transaction. The transaction will fail if the subkey is missing.
        /// </summary>
        /// <remarks>The effect of any previous Require* call on the same subkey
        /// within this transaction will be overwritten by this call.</remarks>
        public void RequirePresentSubkey(InternedBlobRef key, ulong subkey)
        {
            PInvoke_RequirePresentSubkey(handle, key.handle, subkey);
        }

        /// <summary>
        /// Instructs the transaction to check that the subkey is missing before
        /// applying the transaction. The transaction will fail if the subkey is present.
        /// </summary>
        /// <remarks>The effect of any previous Require* call on the same subkey
        /// within this transaction will be overwritten by this call.</remarks>
        public void RequireMissingSubkey(InternedBlobRef key, ulong subkey)
        {
            PInvoke_RequireMissingSubkey(handle, key.handle, subkey);
        }

        /// <summary>
        /// Instructs the transaction to check that the subkey has the provided value before
        /// applying the transaction.
        /// The transaction will fail if the value is different, or the subkey is missing.
        /// </summary>
        /// <remarks>The effect of any previous Require* call on the same subkey
        /// within this transaction will be overwritten by this call.</remarks>
        public void RequireValue(InternedBlobRef key, ulong subkey, ValueRef requiredValue)
        {
            PInvoke_RequireValue(handle, key.handle, subkey, requiredValue.handle);
        }

        /// <summary>
        /// Instructs the transaction to check that the subkey has the provided version before
        /// applying the transaction.
        /// The transaction will fail if the version is different, or the subkey is missing.
        /// </summary>
        /// <remarks>The effect of any previous Require* call on the same subkey
        /// within this transaction will be overwritten by this call.</remarks>
        public void RequireVersion(InternedBlobRef key, ulong subkey, ulong requiredVersion)
        {
            PInvoke_RequireVersion(handle, key.handle, subkey, requiredVersion);
        }

        /// <summary>
        /// Instructs the transaction to check that the number of subkeys of the key
        /// is equal to the provided number.
        /// The transaction will fail if the number of subkeys is different.
        /// </summary>
        /// <remarks>Any number is allowed, including 0 (to require that the entire key is missing).
        /// The effect of any previous Require* call on the same subkey
        /// within this transaction will be overwritten by this call.</remarks>
        public void RequireSubkeysCount(InternedBlobRef key, ulong requiredSubkeysCount)
        {
            PInvoke_RequireSubkeysCount(handle, key.handle, requiredSubkeysCount);
        }

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_Create")]
        private static extern unsafe IntPtr PInvoke_Create();

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_Put")]
        private static extern void PInvoke_Put(IntPtr builderHandle, IntPtr keyHandle, ulong subkey, IntPtr valueHandle);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
        "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_PutBytes")]
        private static extern unsafe void PInvoke_PutBytes(IntPtr builderHandle, IntPtr keyHandle, ulong subkey, void* data, int size);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_DeleteSubkey")]
        private static extern void PInvoke_DeleteSubkey(IntPtr builderHandle, IntPtr keyHandle, ulong subkey);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_DeleteKey")]
        private static extern void PInvoke_DeleteKey(IntPtr builderHandle, IntPtr keyHandle);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_RequirePresentSubkey")]
        private static extern void PInvoke_RequirePresentSubkey(IntPtr builderHandle, IntPtr keyHandle, ulong subkey);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_RequireMissingSubkey")]
        private static extern void PInvoke_RequireMissingSubkey(IntPtr builderHandle, IntPtr keyHandle, ulong subkey);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_RequireValue")]
        private static extern void PInvoke_RequireValue(IntPtr builderHandle, IntPtr keyHandle, ulong subkey, IntPtr valueHandle);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_RequireVersion")]
        private static extern void PInvoke_RequireVersion(IntPtr builderHandle, IntPtr keyHandle, ulong subkey, ulong requiredVersion);

        [DllImport(PInvokeAPI.StateSyncLibraryName, EntryPoint =
            "Microsoft_MixedReality_Sharing_StateSync_TransactionBuilder_RequireSubkeysCount")]
        private static extern void PInvoke_RequireSubkeysCount(IntPtr builderHandle, IntPtr keyHandle, ulong subkeysCount);
    }
}
