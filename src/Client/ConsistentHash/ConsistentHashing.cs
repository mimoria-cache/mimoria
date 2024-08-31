// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

namespace Varelen.Mimoria.Client.ConsistentHash;

/// <summary>
/// Simple and thread-safe implementation of consistent hashing (click <see href="https://en.wikipedia.org/wiki/Consistent_hashing">here</see> for more information).
/// </summary>
public sealed class ConsistentHashing : IConsistentHashing
{
    private readonly ReaderWriterLockSlim readerWriterLockSlim = new();
    private readonly IHasher hasher;
    private readonly Dictionary<uint, Guid> hashServerIds = [];
    private readonly SortedSet<uint> hashRange = [];

    public ConsistentHashing(IHasher hasher)
        : this(hasher, [])
    {

    }

    public ConsistentHashing(IHasher hasher, IEnumerable<Guid> serverIds)
    {
        this.hasher = hasher;
        foreach (Guid serverId in serverIds)
        {
            this.AddServerId(serverId);
        }
    }

    public void AddServerId(Guid serverId)
    {
        this.readerWriterLockSlim.EnterWriteLock();

        try
        {
            uint serverHash = this.hasher.Hash(serverId.ToString());

            this.hashServerIds.Add(serverHash, serverId);
            this.hashRange.Add(serverHash);
        }
        finally
        {
            this.readerWriterLockSlim.ExitWriteLock();
        }
    }

    public void RemoveServerId(Guid serverId)
    {
        this.readerWriterLockSlim.EnterWriteLock();

        try
        {
            uint serverHash = this.hasher.Hash(serverId.ToString());

            this.hashServerIds.Remove(serverHash);
            this.hashRange.Remove(serverHash);
        }
        finally
        {
            this.readerWriterLockSlim.ExitWriteLock();
        }
    }

    public Guid GetServerId(string key)
    {
        this.readerWriterLockSlim.EnterReadLock();

        try
        {
            uint keyHash = this.hasher.Hash(key);
            uint serverHash = this.FindServerHash(keyHash);
            return serverHash != 0 ? hashServerIds[serverHash] : hashServerIds.Values.First();
        }
        finally
        {
            this.readerWriterLockSlim.ExitReadLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint FindServerHash(uint keyHash)
    {
        foreach (uint hashSpaceHash in this.hashRange)
        {
            if (hashSpaceHash > keyHash)
            {
                return hashSpaceHash;
            }
        }
        return default;
    }
}
