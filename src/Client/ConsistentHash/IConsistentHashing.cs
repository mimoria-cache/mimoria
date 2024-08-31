// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.ConsistentHash;

public interface IConsistentHashing
{
    void AddServerId(Guid serverId);
    void RemoveServerId(Guid serverId);
    Guid GetServerId(string key);
}
