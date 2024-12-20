// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Network;

namespace Varelen.Mimoria.Server.PubSub;

public sealed class PubSubService : IPubSubService
{
    private readonly ILogger<PubSubService> logger;
    private readonly Dictionary<string, List<TcpConnection>> subscriptions;
    private readonly ReaderWriterLockSlim subscriptionsReadWriteLock;

    public PubSubService(ILogger<PubSubService> logger)
    {
        this.logger = logger;
        this.subscriptions = [];
        this.subscriptionsReadWriteLock = new ReaderWriterLockSlim();
    }

    public void Subscribe(string channel, TcpConnection tcpConnection)
    {
        this.subscriptionsReadWriteLock.EnterWriteLock();

        try
        {
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

            this.subscriptions.Add(channel, [tcpConnection]);
        }
        finally
        {
            this.subscriptionsReadWriteLock.ExitWriteLock();
        }
    }

    public void Unsubscribe(TcpConnection tcpConnection)
    {
        this.subscriptionsReadWriteLock.EnterWriteLock();

        try
        {
            // Just try to remove the connection from all channels
            int unsubscripedChannels = 0;
            foreach (var (_, tcpConnections) in this.subscriptions)
            {
                if (tcpConnections.Remove(tcpConnection))
                {
                    unsubscripedChannels++;
                }
            }

            this.logger.LogInformation("Connection '{RemoteEndPoint}' unsubscribed from '{ChannelCount}' channels", tcpConnection.RemoteEndPoint, unsubscripedChannels);
        }
        finally
        {
            this.subscriptionsReadWriteLock.ExitWriteLock();
        }
    }

    public void Unsubscribe(string channel, TcpConnection tcpConnection)
    {
        this.subscriptionsReadWriteLock.EnterWriteLock();

        try
        {
            this.logger.LogInformation("Connection '{RemoteEndPoint}' unsubscribed from channel '{Channel}'", tcpConnection.RemoteEndPoint, channel);

            if (!this.subscriptions.TryGetValue(channel, out List<TcpConnection>? tcpConnections))
            {
                return;
            }

            tcpConnections.Remove(tcpConnection);
        }
        finally
        {
            this.subscriptionsReadWriteLock.ExitWriteLock();
        }
    }

    public async ValueTask PublishAsync(string channel, MimoriaValue payload)
    {
        this.subscriptionsReadWriteLock.EnterReadLock();

        try
        {
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
        finally
        {
            this.subscriptionsReadWriteLock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        this.subscriptions.Clear();
        this.subscriptionsReadWriteLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
