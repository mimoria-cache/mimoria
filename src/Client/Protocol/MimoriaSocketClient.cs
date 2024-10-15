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
    private readonly IRetryPolicy operationRetryPolicy;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer>> taskCompletionSources;

    /// <summary>
    /// Create a new Mimoria socket client with the default timeout of 250 milliseconds.
    /// </summary>
    public MimoriaSocketClient()
        : this(DefaultOperationTimeout)
    {

    }

    public MimoriaSocketClient(TimeSpan operationTimeout)
        : this(operationTimeout, new ExponentialRetryPolicy(initialDelay: 1000, maxRetries: 4, typeof(TimeoutException)))
    {

    }

    public MimoriaSocketClient(TimeSpan operationTimeout, IRetryPolicy operationRetryPolicy)
    {
        this.operationTimeout = operationTimeout;
        this.operationRetryPolicy = operationRetryPolicy;
        this.taskCompletionSources = new ConcurrentDictionary<uint, TaskCompletionSource<IByteBuffer>>();
    }

    protected override void OnPacketReceived(IByteBuffer byteBuffer)
    {
        _ = (Operation)byteBuffer.ReadByte();

        uint requestId = byteBuffer.ReadUInt();
        var statusCode = (StatusCode)byteBuffer.ReadByte();

        if (!this.taskCompletionSources.TryRemove(requestId, out TaskCompletionSource<IByteBuffer>? taskCompletionSource))
        {
            return;
        }

        if (statusCode == StatusCode.Error)
        {
            string errorText = byteBuffer.ReadString()!;
            taskCompletionSource.SetException(new MimoriaErrorStatusCodeException(errorText));
            return;
        }

        taskCompletionSource.SetResult(byteBuffer);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<IByteBuffer> AddResponseTask(uint requestId)
    {
        var taskCompletionSource = new TaskCompletionSource<IByteBuffer>();
        this.taskCompletionSources[requestId] = taskCompletionSource;
        return taskCompletionSource.Task;
    }
}
