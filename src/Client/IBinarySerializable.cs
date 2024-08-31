// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client;

public interface IBinarySerializable
{
    void Serialize(IByteBuffer byteBuffer);
    void Deserialize(IByteBuffer byteBuffer);
}
