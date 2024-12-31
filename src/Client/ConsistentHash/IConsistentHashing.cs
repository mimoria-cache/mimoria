// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Client.ConsistentHash;

public interface IConsistentHashing
{
    void AddServerId(int serverId);
    void RemoveServerId(int serverId);
    int GetServerId(string key);
}
