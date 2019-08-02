// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Utilities.IO
{
    public abstract class OutputStreamBase : StreamBase
    {
        public sealed override bool CanWrite => false;

        public sealed override int WriteTimeout
        {
            get => throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
            set => throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        public sealed override void Flush()
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        public sealed override void SetLength(long value)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        public sealed override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        protected sealed override Task OnWriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        public sealed override void WriteByte(byte value)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }

        public sealed override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException($"{GetType().Name} can only be used for reading,");
        }
    }
}
