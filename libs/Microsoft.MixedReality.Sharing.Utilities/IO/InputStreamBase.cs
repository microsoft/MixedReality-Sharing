// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Utilities.IO
{
    public abstract class InputStreamBase : StreamBase
    {
        public sealed override bool CanRead => false;

        public sealed override int ReadTimeout
        {
            get => throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
            set => throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
        }

        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
        }

        public sealed override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        protected sealed override Task<int> OnReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
        }

        public sealed override int ReadByte()
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
        }

        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
        }

        public sealed override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for writing,");
        }
    }
}
