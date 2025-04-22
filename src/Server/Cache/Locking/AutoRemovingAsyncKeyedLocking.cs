// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Server.Cache.Locking;

/// <summary>
/// An implementation of async keyed locking based on a string key using <see cref="SemaphoreSlim"/> and <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// 
/// This implementation is based on the library <see href="https://github.com/MarkCiliaVincenti/AsyncKeyedLock" >AsyncKeyedLock</see> from Mark Cilia Vincenti.
/// 
/// MIT License
/// 
/// Copyright(c) 2024 Mark Cilia Vincenti
/// Copyright(c) 2025 varelen
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.
/// </summary>
public sealed class AutoRemovingAsyncKeyedLocking : IDisposable
{
    private readonly ConcurrentDictionary<string, ReferenceCountedReleaser> releasersByKey;

    public AutoRemovingAsyncKeyedLocking(int initialCapacity)
        => this.releasersByKey = new(Environment.ProcessorCount, capacity: initialCapacity, StringComparer.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<ReferenceCountedReleaser?> LockAsync(string key, bool takeLock = true)
    {
        if (!takeLock)
        {
            return null;
        }

        if (this.releasersByKey.TryGetValue(key, out ReferenceCountedReleaser? existingReleaser)
            && existingReleaser.TryRetain())
        {
            return await WaitAsync(existingReleaser);
        }

        var newReleaser = new ReferenceCountedReleaser(this, key);
        if (this.releasersByKey.TryAdd(key, newReleaser))
        {
            return await WaitAsync(newReleaser);
        }

        return await this.GetOrAddReleaser(key, newReleaser);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<ReferenceCountedReleaser> GetOrAddReleaser(string key, ReferenceCountedReleaser newReleaser)
    {
        do
        {
            ReferenceCountedReleaser maybeUseableReleaser = this.releasersByKey.GetOrAdd(key, newReleaser);
            if (maybeUseableReleaser == newReleaser || maybeUseableReleaser.TryRetain())
            {
                return await WaitAsync(maybeUseableReleaser);
            }
        } while (true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static async Task<ReferenceCountedReleaser> WaitAsync(ReferenceCountedReleaser releaser)
    {
        await releaser.Semaphore.WaitAsync();
        return releaser;
    }

    /// <summary>
    /// Used to assert if the key has an active releaser with lock.
    /// 
    /// It would be better to also check if it's the same thread, but it's better than nothing.
    /// </summary>
    /// <param name="key">The key to check for.</param>
    [Conditional("DEBUG")]
    internal void DebugAssertKeyHasReleaserLock(string key)
    {
        Debug.Assert(this.releasersByKey.ContainsKey(key), $"Key '{key}' has no active releaser");
        Debug.Assert(this.releasersByKey[key].Semaphore.CurrentCount == 0, $"Semaphore current count is not zero for releaser with key '{key}'");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasActiveLock(string key)
        => this.releasersByKey.ContainsKey(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(ReferenceCountedReleaser releaser)
    {
        lock (releaser.Locker)
        {
            if (releaser.TryLose())
            {
                if (this.releasersByKey.TryRemove(releaser.Key, out var removedReleaser))
                {
                    Debug.Assert(removedReleaser == releaser, "Removed releaser does not match releaser");
                }

                int previousCount = releaser.Semaphore.Release();
                Debug.Assert(previousCount == 0, "Sempahore previous count was not zero");
                return;
            }
        }

        int outerPreviousCount = releaser.Semaphore.Release();
        Debug.Assert(outerPreviousCount == 0, "Sempahore outer previous count was not zero");
    }

    public void Dispose()
    {

    }
}
