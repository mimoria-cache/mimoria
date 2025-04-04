﻿// SPDX-FileCopyrightText: 2024 varelen
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
    private readonly Dictionary<uint, int> hashServerIds;
    private readonly SortedSet<uint> hashRange;

    public ConsistentHashing(IHasher hasher)
        : this(hasher, Enumerable.Empty<int>())
    {

    }

    public ConsistentHashing(IHasher hasher, IEnumerable<int> serverIds)
    {
        this.hasher = hasher;
        this.hashServerIds = new Dictionary<uint, int>();
        this.hashRange = new SortedSet<uint>();
        
        foreach (int serverId in serverIds)
        {
            this.AddServerId(serverId);
        }
    }

    public void AddServerId(int serverId)
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

    public void RemoveServerId(int serverId)
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

    public int GetServerId(string key)
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
