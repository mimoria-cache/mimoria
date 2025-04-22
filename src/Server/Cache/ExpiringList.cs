// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Varelen.Mimoria.Server.Cache;

/// <summary>
/// Simple implementation of a list with expiring values.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
public sealed class ExpiringList<TValue>
{
    private const int DefaultCapacity = 20;

    private readonly List<ListEntry<TValue>> list;

    public int Count => this.list.Count;

    public ExpiringList(int capacity)
        => this.list = new List<ListEntry<TValue>>(capacity);

    public ExpiringList(TValue initialValue, uint ttl)
    {
        this.list = new List<ListEntry<TValue>>(capacity: DefaultCapacity)
        {
            new(initialValue, ttl)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RemoveExpired()
        => this.list.RemoveAll(x => x.Expired);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TValue value, uint ttl)
        => this.list.Add(new ListEntry<TValue>(value, ttl));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(TValue value)
    {
        if (this.list.Count == 0)
        {
            return false;
        }

        int index = this.list.IndexOf(new ListEntry<TValue>(value));
        if (index == -1)
        {
            return false;
        }

        if (this.list[index].Expired)
        {
            this.list.RemoveAt(index);
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TValue value)
        => this.list.Remove(new ListEntry<TValue>(value));

    public IEnumerable<(TValue Value, uint Ttl)> Get()
    {
        foreach (ListEntry<TValue> listEntry in this.list)
        {
            if (listEntry.Expired)
            {
                continue;
            }

            yield return (listEntry.Value, listEntry.TtlMilliseconds);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct ListEntry<TValueEntry>
    {
        public TValueEntry Value { get; }

        public DateTime Added { get; } = DateTime.UtcNow;

        public uint TtlMilliseconds { get; } = ExpiringDictionaryCache.InfiniteTimeToLive;

        public bool Expired => this.TtlMilliseconds != ExpiringDictionaryCache.InfiniteTimeToLive && (DateTime.UtcNow - this.Added).TotalMilliseconds >= this.TtlMilliseconds;

        public ListEntry(TValueEntry value, uint ttlMilliseconds)
        {
            this.Value = value;
            this.TtlMilliseconds = ttlMilliseconds;
        }

        internal ListEntry(TValueEntry value)
            => this.Value = value;

        public override bool Equals(object? other)
            => other is ListEntry<TValueEntry> entry &&
                this.Value?.Equals(entry.Value) == true;

        public override int GetHashCode()
            => this.Value?.GetHashCode() ?? -1;
    }
}
