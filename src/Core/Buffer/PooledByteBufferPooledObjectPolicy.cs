// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.ObjectPool;

namespace Varelen.Mimoria.Core.Buffer;

public sealed class PooledByteBufferPooledObjectPolicy : IPooledObjectPolicy<PooledByteBuffer>
{
    public PooledByteBuffer Create()
        => new();

    public bool Return(PooledByteBuffer pooledByteBuffer)
    {
        pooledByteBuffer.Reset();
        return true;
    }
}
