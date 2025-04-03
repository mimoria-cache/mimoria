// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.ObjectPool;

namespace Varelen.Mimoria.Core.Buffer;

/// <summary>
/// A policy for creating and returning <see cref="PooledByteBuffer"/> instances.
/// </summary>
public sealed class PooledByteBufferPooledObjectPolicy : IPooledObjectPolicy<PooledByteBuffer>
{
    /// <inheritdoc />
    public PooledByteBuffer Create()
        => new();

    /// <inheritdoc />
    public bool Return(PooledByteBuffer pooledByteBuffer)
    {
        pooledByteBuffer.Reset();
        return true;
    }
}
