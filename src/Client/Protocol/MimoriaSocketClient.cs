// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Client.Retry;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Client.Network;

namespace Varelen.Mimoria.Client.Protocol;

public sealed class MimoriaSocketClient : AsyncTcpSocketClient, IMimoriaSocketClient
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan operationTimeout;
    private readonly IRetryPolicy<IByteBuffer> operationRetryPolicy;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer>> taskCompletionSources;
    private readonly ConcurrentDictionary<string, List<Subscription>> subscriptions;
    private readonly ReaderWriterLockSlim subscriptionsReadWriteLock;

    ICollection<string> IMimoriaSocketClient.SubscribedChannels => this.subscriptions.Keys;

    /// <summary>
    /// Create a new Mimoria socket client with the default timeout of 250 milliseconds.
    /// </summary>
    public MimoriaSocketClient()
        : this(DefaultOperationTimeout)
    {

    }

    public MimoriaSocketClient(TimeSpan operationTimeout)
        : this(operationTimeout, new ExponentialRetryPolicy<IByteBuffer>(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException)))
    {

    }

    public MimoriaSocketClient(TimeSpan operationTimeout, IRetryPolicy<IByteBuffer> operationRetryPolicy)
    {
        this.operationTimeout = operationTimeout;
        this.operationRetryPolicy = operationRetryPolicy;
        this.taskCompletionSources = new ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer>>();
        this.subscriptions = new ConcurrentDictionary<string, List<Subscription>>();
        this.subscriptionsReadWriteLock = new ReaderWriterLockSlim();
    }

    protected override void OnPacketReceived(IByteBuffer byteBuffer)
    {
        var operation = (Operation)byteBuffer.ReadByte();
        if (operation == Operation.Publish)
        {
            string channel = byteBuffer.ReadString()!;

            this.subscriptionsReadWriteLock.EnterReadLock();

            try
            {
                if (!this.subscriptions.TryGetValue(channel, out List<Subscription>? foundSubscriptions))
                {
                    return;
                }

                MimoriaValue payload = byteBuffer.ReadValue();

                foreach (Subscription subscription in foundSubscriptions)
                {
                    subscription.OnPayload(payload);
                }
            }
            finally
            {
                this.subscriptionsReadWriteLock.ExitReadLock();
                byteBuffer.Dispose();
            }

            return;
        }

        uint requestId = byteBuffer.ReadUInt();
        var statusCode = (StatusCode)byteBuffer.ReadByte();

        if (!this.taskCompletionSources.TryRemove(requestId, out TaskCompletionSource<IByteBuffer>? taskCompletionSource))
        {
            byteBuffer.Dispose();
            return;
        }

        if (statusCode == StatusCode.Error)
        {
            string errorText = byteBuffer.ReadString()!;
            taskCompletionSource.SetException(new MimoriaErrorStatusCodeException(errorText));
            byteBuffer.Dispose();
            return;
        }

        taskCompletionSource.SetResult(byteBuffer);
    }

    protected override void HandleDisconnect(bool force)
    {
        this.taskCompletionSources.Clear();

        if (!force)
        {
            return;
        }

        this.subscriptions.Clear();
    }

    public async Task<IByteBuffer> SendAndWaitForResponseAsync(uint requestId, IByteBuffer byteBuffer, CancellationToken cancellationToken = default)
    {
        try
        {
            Task<IByteBuffer> responseTask = this.AddResponseTask(requestId);
            return await this.operationRetryPolicy.ExecuteAsync(async () =>
            {
                byteBuffer.Retain();
                await this.SendAsync(byteBuffer, cancellationToken);
                return await responseTask.WaitAsync(this.operationTimeout, cancellationToken);
            }, cancellationToken);
        }
        finally
        {
            byteBuffer.Dispose();
        }
    }

    public ValueTask SendAndForgetAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default)
        => this.SendAsync(byteBuffer, cancellationToken);

    public (Subscription, bool alreadySubscribed) Subscribe(string channel)
    {
        this.subscriptionsReadWriteLock.EnterWriteLock();

        try
        {
            if (this.subscriptions.TryGetValue(channel, out List<Subscription>? foundSubscriptions))
            {
                var subscription = new Subscription();
                foundSubscriptions.Add(subscription);
                return (subscription, alreadySubscribed: true);
            }

            var newSubscription = new Subscription();
            this.subscriptions.TryAdd(channel, new List<Subscription> { newSubscription });
            return (newSubscription, alreadySubscribed: false);
        }
        finally
        {
            this.subscriptionsReadWriteLock.ExitWriteLock();
        }
    }

    public bool Unsubscribe(string channel)
    {
        this.subscriptionsReadWriteLock.EnterWriteLock();

        try
        {
            return this.subscriptions.TryRemove(channel, out _);
        }
        finally
        {
            this.subscriptionsReadWriteLock.ExitWriteLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<IByteBuffer> AddResponseTask(uint requestId)
    {
        var taskCompletionSource = new TaskCompletionSource<IByteBuffer>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.taskCompletionSources[requestId] = taskCompletionSource;
        return taskCompletionSource.Task;
    }
}
