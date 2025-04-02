// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Varelen.Mimoria.Client.Network;
using Varelen.Mimoria.Client.Retry;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client.Protocol;

/// <summary>
/// A TCP socket client implementation that communicates with a Mimoria server.
/// </summary>
public sealed class MimoriaSocketClient : AsyncTcpSocketClient, IMimoriaSocketClient
{
    private static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMilliseconds(250);

    private readonly TimeSpan operationTimeout;
    private readonly IRetryPolicy<IByteBuffer> operationRetryPolicy;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer>> taskCompletionSources;
    private readonly ConcurrentDictionary<string, List<Subscription>> subscriptions;
    private readonly ReaderWriterLockSlim subscriptionsReadWriteLock;
    private readonly Channel<(string channel, MimoriaValue payload)> publishChannel;

    ICollection<(string Channel, List<Subscription> Subscriptions)> IMimoriaSocketClient.Subscriptions => this.subscriptions.Select(keyValue => (keyValue.Key, keyValue.Value)).ToList();

    /// <summary>
    /// Creates a new Mimoria socket client with the default timeout of 250 milliseconds.
    /// </summary>
    public MimoriaSocketClient()
        : this(DefaultOperationTimeout)
    {

    }

    /// <summary>
    /// Creates a new Mimoria socket client with the specified operation timeout.
    /// </summary>
    public MimoriaSocketClient(TimeSpan operationTimeout)
        : this(operationTimeout, new ExponentialRetryPolicy<IByteBuffer>(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException)))
    {

    }

    /// <summary>
    /// Creates a new Mimoria socket client with the specified operation timeout and operation retry policy.
    /// </summary>
    public MimoriaSocketClient(TimeSpan operationTimeout, IRetryPolicy<IByteBuffer> operationRetryPolicy)
    {
        this.operationTimeout = operationTimeout;
        this.operationRetryPolicy = operationRetryPolicy;
        this.taskCompletionSources = new ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer>>();
        this.subscriptions = new ConcurrentDictionary<string, List<Subscription>>();
        this.subscriptionsReadWriteLock = new ReaderWriterLockSlim();
        this.publishChannel = Channel.CreateUnbounded<(string channel, MimoriaValue payload)>();

        _ = Task.Factory.StartNew(this.ProcessPublishesAsync, TaskCreationOptions.LongRunning);
    }

    private async Task ProcessPublishesAsync()
    {
        try
        {
            await foreach ((string channel, MimoriaValue payload) in this.publishChannel.Reader.ReadAllAsync())
            {
                if (!this.subscriptions.TryGetValue(channel, out List<Subscription>? foundSubscriptions))
                {
                    continue;
                }

                this.subscriptionsReadWriteLock.EnterReadLock();

                try
                {
                    foreach (Subscription subscription in foundSubscriptions)
                    {
                        subscription.OnPayload(payload);
                    }
                }
                finally
                {
                    this.subscriptionsReadWriteLock.ExitReadLock();
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"An error occurred while reading from the publish channel: {exception}");
        }
    }

    /// <inheritdoc />
    protected override void OnPacketReceived(IByteBuffer byteBuffer)
    {
        var operation = (Operation)byteBuffer.ReadByte();
        if (operation == Operation.Publish)
        {
            string channel = byteBuffer.ReadString()!;

            if (!this.subscriptions.TryGetValue(channel, out _))
            {
                return;
            }

            bool written = this.publishChannel.Writer.TryWrite((channel, byteBuffer.ReadValue()));
            Debug.Assert(written, "Publish was not written to the publish channel");

            byteBuffer.Dispose();
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

    /// <inheritdoc />
    protected override void HandleDisconnect(bool force)
    {
        this.taskCompletionSources.Clear();

        if (!force)
        {
            return;
        }

        this.subscriptions.Clear();
        this.publishChannel.Writer.Complete();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ValueTask SendAndForgetAsync(IByteBuffer byteBuffer, CancellationToken cancellationToken = default)
        => this.SendAsync(byteBuffer, cancellationToken);

    /// <inheritdoc />
    public (Subscription, bool AlreadySubscribed) Subscribe(string channel)
    {
        this.subscriptionsReadWriteLock.EnterWriteLock();

        try
        {
            if (this.subscriptions.TryGetValue(channel, out List<Subscription>? foundSubscriptions))
            {
                var subscription = new Subscription();
                foundSubscriptions.Add(subscription);
                return (subscription, AlreadySubscribed: true);
            }

            var newSubscription = new Subscription();
            this.subscriptions.TryAdd(channel, new List<Subscription> { newSubscription });
            return (newSubscription, AlreadySubscribed: false);
        }
        finally
        {
            this.subscriptionsReadWriteLock.ExitWriteLock();
        }
    }

    void IMimoriaSocketClient.SubscribeInternal(string channel, List<Subscription> subscriptions)
        => this.subscriptions[channel] = subscriptions;

    /// <inheritdoc />
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
