// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Cache.Locking;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.PubSub;

public sealed class PubSubService : IPubSubService
{
    // Prime number to favor the dictionary implementation
    private const int InitialCacheSize = 503;

    private readonly ILogger<PubSubService> logger;
    private readonly ConcurrentDictionary<string, List<TcpConnection>> subscriptions;
    private readonly AutoRemovingAsyncKeyedLocking autoRemovingAsyncKeyedLocking;

    public PubSubService(ILogger<PubSubService> logger)
    {
        this.logger = logger;
        this.subscriptions = [];
        this.autoRemovingAsyncKeyedLocking = new AutoRemovingAsyncKeyedLocking(InitialCacheSize);
    }

    public async Task SubscribeAsync(string channel, TcpConnection tcpConnection)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(channel);

        this.logger.LogInformation("Connection '{RemoteEndPoint}' subscribed to channel '{Channel}'", tcpConnection.RemoteEndPoint, channel);

        if (this.subscriptions.TryGetValue(channel, out List<TcpConnection>? tcpConnections))
        {
            if (tcpConnections.Contains(tcpConnection))
            {
                // Subscribing to a channel twice is a noop
                return;
            }

            tcpConnections.Add(tcpConnection);
            return;
        }

        this.subscriptions.TryAdd(channel, [tcpConnection]);
    }

    public async Task UnsubscribeAsync(TcpConnection tcpConnection)
    {
        // Just try to remove the connection from all channels
        int unsubscribedChannels = 0;
        foreach (var (channel, tcpConnections) in this.subscriptions)
        {
            using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(channel);

            if (tcpConnections.Remove(tcpConnection))
            {
                unsubscribedChannels++;
            }
        }

        this.logger.LogInformation("Connection '{RemoteEndPoint}' unsubscribed from '{ChannelCount}' channels", tcpConnection.RemoteEndPoint, unsubscribedChannels);
    }

    public async Task UnsubscribeAsync(string channel, TcpConnection tcpConnection)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(channel);

        this.logger.LogInformation("Connection '{RemoteEndPoint}' unsubscribed from channel '{Channel}'", tcpConnection.RemoteEndPoint, channel);

        if (!this.subscriptions.TryGetValue(channel, out List<TcpConnection>? tcpConnections))
        {
            return;
        }

        tcpConnections.Remove(tcpConnection);
    }

    public async Task PublishAsync(string channel, MimoriaValue payload)
    {
        using var releaser = await this.autoRemovingAsyncKeyedLocking.LockAsync(channel);

        if (!this.subscriptions.TryGetValue(channel, out List<TcpConnection>? tcpConnections))
        {
            return;
        }

        using IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Publish);
        byteBuffer.WriteString(channel);
        byteBuffer.WriteValue(payload);
        byteBuffer.EndPacket();

        foreach (TcpConnection tcpConnection in tcpConnections)
        {
            byteBuffer.Retain();
            await tcpConnection.SendAsync(byteBuffer);
        }
    }

    public void Dispose()
    {
        this.subscriptions.Clear();
        this.autoRemovingAsyncKeyedLocking.Dispose();
        GC.SuppressFinalize(this);
    }
}
