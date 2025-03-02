// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Server.Cache.Locking;

/// <summary>
/// An implementation of a reference counted releaser for handling the automatic release of a keyed lock.
/// </summary>
public sealed class ReferenceCountedReleaser : IDisposable
{
    private readonly AutoRemovingAsyncKeyedLocking autoRemovingAsyncKeyedLocking;
    private readonly SemaphoreSlim semaphoreSlim = new(initialCount: 1, maxCount: 1);
    private readonly string key;
    private readonly Lock locker = new();

    private int referenceCount;

    public SemaphoreSlim Semaphore
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.semaphoreSlim;
    }

    public string Key
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.key;
    }

    public Lock Locker
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.locker;
    }

    public ReferenceCountedReleaser(AutoRemovingAsyncKeyedLocking autoRemovingAsyncKeyedLocking, string key)
    {
        this.autoRemovingAsyncKeyedLocking = autoRemovingAsyncKeyedLocking;
        this.key = key;
        this.referenceCount = 1;
    }

    /// <summary>
    /// Tries to retain the releaser by incrementing the reference count.
    /// </summary>
    /// <returns>true if retained, otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRetain()
    {
        if (this.locker.TryEnter())
        {
            // This releaser was already removed, so don't use it
            if (this.referenceCount == 0)
            {
                this.locker.Exit();
                return false;
            }

            this.referenceCount++;
            this.locker.Exit();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Decrements the reference count and returns true if the reference count reached zero.
    /// </summary>
    /// <returns>true if the reference count is zero (releaser can be removed), otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryLose()
    {
        Debug.Assert(this.locker.IsHeldByCurrentThread);
        return --this.referenceCount == 0;
    }

    /// <summary>
    /// This disposes the releaser and releases the lock.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
        => this.autoRemovingAsyncKeyedLocking.Release(this);
}
