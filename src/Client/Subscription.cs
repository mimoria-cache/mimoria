// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public sealed class Subscription
{
    public delegate void OnPayloadEvent(MimoriaValue payload);

    public event OnPayloadEvent? Payload;

    public void OnPayload(MimoriaValue payload)
        => this.Payload?.Invoke(payload);
}
