// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Server.Cache;

public enum CacheValueType : byte
{
    Null = 0,
    String = 1,
    List = 2,
    Map = 3,
    Counter = 4,
    Bytes = 5
}
