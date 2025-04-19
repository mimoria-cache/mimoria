// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Bully;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Cache.Locking;
using Varelen.Mimoria.Server.Cluster;
using Varelen.Mimoria.Server.Extensions;
using Varelen.Mimoria.Server.Metrics;
using Varelen.Mimoria.Server.Network;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;
using Varelen.Mimoria.Server.Replication;

namespace Varelen.Mimoria.Server;

public sealed class MimoriaServer : IMimoriaServer
{
    private const uint ProtocolVersion = 2;

    private readonly ILogger<MimoriaServer> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly IOptionsMonitor<MimoriaOptions> monitor;
    private readonly IPubSubService pubSubService;
    private readonly IMimoriaSocketServer mimoriaSocketServer;
    private readonly ICache cache;
    private readonly IMimoriaMetrics metrics;
    private readonly IReplicator? replicator;

    private readonly ClusterServer? clusterServer;
    private readonly Dictionary<int, ClusterClient> clusterClients;

    private DateTime startDateTime;

    private readonly BullyAlgorithm? bullyAlgorithm;
    private readonly TaskCompletionSource nodeReadyTaskCompletionSource;
    private readonly TaskCompletionSource clusterReadyTaskCompletionSource;

    public IBullyAlgorithm? BullyAlgorithm => this.bullyAlgorithm;

    public MimoriaServer(
        ILogger<MimoriaServer> logger,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<MimoriaOptions> monitor,
        IPubSubService pubSubService,
        IMimoriaSocketServer mimoriaSocketServer,
        ICache cache,
        IMimoriaMetrics metrics)
    {
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.monitor = monitor;
        this.pubSubService = pubSubService;
        this.mimoriaSocketServer = mimoriaSocketServer;
        this.cache = cache;
        this.metrics = metrics;
        this.clusterClients = [];
        this.nodeReadyTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.clusterReadyTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        if (this.monitor.CurrentValue.Cluster is not null)
        {
            this.clusterServer = new ClusterServer(this.loggerFactory.CreateLogger<ClusterServer>(), this.loggerFactory.CreateLogger<ClusterConnection>(), this.monitor.CurrentValue.Cluster.Ip, this.monitor.CurrentValue.Cluster?.Port ?? 0, this.monitor.CurrentValue.Cluster?.Nodes?.Length ?? 0, this.monitor.CurrentValue.Cluster?.Password!, this.cache);
            this.clusterServer.AllClientsConnected += HandleAllClientsConnected;
            this.clusterServer.AliveReceived += HandleAliveReceived;
            this.clusterServer.Start();

            this.bullyAlgorithm = new BullyAlgorithm(
                this.loggerFactory.CreateLogger<BullyAlgorithm>(),
                this.monitor.CurrentValue.Cluster!.Id,
                this.monitor.CurrentValue.Cluster!.Nodes.Select(n => n.Id!.Value).ToArray(),
                this.clusterServer,
                TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Election.LeaderHeartbeatIntervalMs),
                TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Election.LeaderMissingTimeoutMs),
                TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Election.ElectionTimeoutMs));
            this.bullyAlgorithm.LeaderElected += this.HandleLeaderElectedAsync;

            this.logger.LogInformation("In cluster mode, using nodes: '{}'", string.Join(',', this.monitor.CurrentValue.Cluster!.Nodes.Select(n => $"{n.Host}:{n.Port}")));

            this.replicator = this.monitor.CurrentValue.Cluster.Replication.Type == MimoriaOptions.ReplicationType.Sync
                ? new SyncReplicator(this.clusterServer, this.bullyAlgorithm)
                : new AsyncReplicator(clusterServer, TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Replication.IntervalMilliseconds!.Value));

            this.logger.LogInformation("Using '{Replicator}' replicator", this.monitor.CurrentValue.Cluster.Replication.Type);
        }
    }

    private void HandleAllClientsConnected()
    {
        if (!this.nodeReadyTaskCompletionSource.Task.IsCompleted)
        {
            this.nodeReadyTaskCompletionSource.SetResult();
        }
    }

    private void HandleAliveReceived(int leader)
    {
        this.bullyAlgorithm?.HandleAlive(leader);
    }

    public async Task StartAsync()
    {
        this.monitor.OnChange(OnOptionsChanged);

        this.RegisterOperationHandlers();

        this.startDateTime = DateTime.UtcNow;

        if (this.monitor.CurrentValue.Cluster is not null)
        {
            foreach (MimoriaOptions.NodeOptions node in this.monitor.CurrentValue.Cluster.Nodes)
            {
                // TODO: Handle DNS error?
                var addresses = await Dns.GetHostAddressesAsync(node.Host!);

                var clusterClient = new ClusterClient(this.loggerFactory.CreateLogger<ClusterClient>(), this.monitor.CurrentValue.Cluster.Id, addresses[0].ToString(), node.Port!.Value, this.bullyAlgorithm!, this.cache, this.monitor.CurrentValue.Cluster.Password!);
                await clusterClient.ConnectAsync();
                
                this.clusterClients.Add(node.Id!.Value, clusterClient);
            }

            this.logger.LogInformation("Waiting for node to be ready");
            await this.nodeReadyTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(15), $"Waiting for node to be ready has timed out ('{this.clusterServer!.Clients.Count}' cluster clients connected)");
            this.logger.LogInformation("Node ready");

            _ = this.bullyAlgorithm!.StartAsync();

            this.logger.LogInformation("Waiting for cluster to be ready");
            await this.clusterReadyTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(15), $"Waiting for cluster to be ready has timed out (is leader: {this.bullyAlgorithm.IsLeader}, leader id: {this.bullyAlgorithm.Leader})");
            this.logger.LogInformation("Cluster ready");
        }

        this.mimoriaSocketServer.Disconnected += HandleTcpConnectionDisconnectedAsync;
        this.mimoriaSocketServer.Start(this.monitor.CurrentValue.Ip, (ushort)this.monitor.CurrentValue.Port, this.monitor.CurrentValue.Backlog);

        this.logger.LogInformation("Mimoria server started on '{Ip}:{Port}'", this.monitor.CurrentValue.Ip, this.monitor.CurrentValue.Port);
    }

    private async Task HandleLeaderElectedAsync(int newLeaderId)
    {
        Debug.Assert(this.bullyAlgorithm is not null, "bullyAlgorithm is null");

        if (!this.clusterReadyTaskCompletionSource.Task.IsCompleted)
        {
        if (!this.bullyAlgorithm!.IsLeader)
        {
            Debug.Assert(this.clusterClients.ContainsKey(this.bullyAlgorithm.Leader));

            this.logger.LogInformation("Sending resync request to leader '{Leader}'", this.bullyAlgorithm.Leader);

            if (this.clusterClients.TryGetValue(this.bullyAlgorithm.Leader, out ClusterClient? leaderClusterClient))
            {
                uint requestId = leaderClusterClient.IncrementRequestId();

                    var syncRequestBuffer = PooledByteBuffer.FromPool(Operation.SyncRequest, requestId);
                syncRequestBuffer.EndPacket();

                // TODO: Awaiting this ends in a deadlock because it sends an operation and waits for it to be received
                // but only can receive it if the sent operation response is received..
                _ = leaderClusterClient.SendAndWaitForResponseAsync(requestId, syncRequestBuffer)
                    .AsTask()
                    .ContinueWith(t =>
                    {
                            this.clusterReadyTaskCompletionSource.SetResult();
                    });
            }
        }
        else
        {
                this.clusterReadyTaskCompletionSource.SetResult();
            }
        }

        await this.pubSubService.PublishAsync(Channels.PrimaryChanged, this.bullyAlgorithm.Leader);
    }

    private async Task HandleTcpConnectionDisconnectedAsync(TcpConnection tcpConnection)
    {
        await this.pubSubService.UnsubscribeAsync(tcpConnection);
    }

    public void Stop()
    {
        if (this.clusterServer is not null)
        {
            this.clusterServer.AllClientsConnected -= HandleAllClientsConnected;
            this.clusterServer.AliveReceived -= HandleAliveReceived;
            this.clusterServer.Stop();
        }

        if (this.bullyAlgorithm is not null)
        {
            this.bullyAlgorithm.LeaderElected -= this.HandleLeaderElectedAsync;
        this.bullyAlgorithm?.Stop();
        }

        this.mimoriaSocketServer.Disconnected -= HandleTcpConnectionDisconnectedAsync;
        // TODO: Async stop
        this.mimoriaSocketServer.StopAsync().GetAwaiter().GetResult();
        this.pubSubService.Dispose();
        this.replicator?.Dispose();
        this.cache.Dispose();

        foreach (var (_, clusterClient) in this.clusterClients)
        {
            clusterClient.Close();
        }

        this.logger.LogInformation("Mimoria server stopped");
    }

    private void OnOptionsChanged(MimoriaOptions cacheServerOptions)
    {
        this.logger.LogInformation("ServerOptions were reloaded");
    }

    private void RegisterOperationHandlers()
    {
        var operationHandlers = new Dictionary<Operation, Func<uint, TcpConnection, IByteBuffer, bool, ValueTask>>
        {
            { Operation.Login, this.HandleLoginAsync },
            { Operation.GetString, this.HandleGetStringAsync },
            { Operation.SetString, this.HandleSetStringAsync },
            { Operation.GetList, this.HandleGetListAsync },
            { Operation.AddList, this.HandleAddListAsync },
            { Operation.RemoveList, this.HandleRemoveListAsync },
            { Operation.ContainsList, this.HandleContainsListAsync },
            { Operation.Exists, this.HandleExistsAsync },
            { Operation.Delete, this.HandleDeleteAsync },
            { Operation.GetObjectBinary, this.HandleGetObjectBinaryAsync },
            { Operation.SetObjectBinary, this.HandleSetObjectBinaryAsync },
            { Operation.GetBytes, this.HandleGetBytesAsync },
            { Operation.SetBytes, this.HandleSetBytesAsync },
            { Operation.SetCounter, this.HandleSetCounterAsync },
            { Operation.IncrementCounter, this.HandleIncrementCounterAsync },
            { Operation.Bulk, this.HandleBulkAsync },
            { Operation.GetMapValue, this.HandleGetMapValueAsync },
            { Operation.SetMapValue, this.HandleSetMapValueAsync },
            { Operation.GetMap, this.HandleGetMapAsync },
            { Operation.SetMap, this.HandleSetMapAsync },
            { Operation.Subscribe, this.HandleSubscribeAsync },
            { Operation.Unsubscribe, this.HandleUnsubscribeAsync },
            { Operation.Publish, this.HandlePublishAsync },
            { Operation.GetStats, this.HandleGetStatsAsync }
        };

        this.mimoriaSocketServer.SetOperationHandlers(operationHandlers);

        this.logger.LogTrace("Operation handlers registered");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask SendOkResponseAsync(TcpConnection tcpConnection, Operation operation, uint requestId, bool fireAndForget)
    {
        if (fireAndForget)
        {
            return ValueTask.CompletedTask;
        }

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(operation, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask HandleLoginAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        uint clientProtocolVersion = byteBuffer.ReadVarUInt();
        if (ProtocolVersion != clientProtocolVersion)
        {
            this.logger.LogWarning("Connection '{RemoteEndPoint}' has protocol version mismatch. Server protocol version is '{ServerProtocolVersion}' and client protocol version is '{ClientProtocolVersion}'", tcpConnection.Socket.RemoteEndPoint, ProtocolVersion, clientProtocolVersion);

            IByteBuffer protocolVersionMismatchBuffer = PooledByteBuffer.FromPool(Operation.Login, requestId, StatusCode.Error);
            protocolVersionMismatchBuffer.WriteString($"Protocol version mismatch. Server expected protocol version '{ProtocolVersion}' but got client protocol version '{clientProtocolVersion}'");
            protocolVersionMismatchBuffer.EndPacket();
            return tcpConnection.SendAsync(protocolVersionMismatchBuffer);
        }
        
        string password = byteBuffer.ReadString()!;

        tcpConnection.Authenticated = this.monitor.CurrentValue.Password!.Equals(password, StringComparison.Ordinal);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Login, requestId, StatusCode.Ok);
        responseBuffer.WriteBool(tcpConnection.Authenticated);
        if (tcpConnection.Authenticated)
        {
            responseBuffer.WriteInt(this.monitor.CurrentValue.Cluster?.Id ?? 0);
            responseBuffer.WriteBool(this.bullyAlgorithm?.IsLeader ?? false);
        }
        responseBuffer.EndPacket();

        if (tcpConnection.Authenticated)
        {
            this.logger.LogInformation("Connection '{RemoteEndPoint}' authenticated", tcpConnection.Socket.RemoteEndPoint);
        }
        else
        {
            this.logger.LogWarning("Connection '{RemoteEndPoint}' tried to authenticate with wrong password", tcpConnection.Socket.RemoteEndPoint);
        }

        return tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleGetStringAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        ByteString? value = await this.cache.GetStringAsync(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetString, requestId, StatusCode.Ok);
        responseBuffer.WriteByteString(value);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleSetStringAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        ByteString? value = byteBuffer.ReadByteString();
        uint ttlMilliseconds = byteBuffer.ReadVarUInt();

        await this.cache.SetStringAsync(key, value, ttlMilliseconds);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateSetStringAsync(key, value, ttlMilliseconds);
        }

        await SendOkResponseAsync(tcpConnection, Operation.SetString, requestId, fireAndForget);
    }

    private async ValueTask HandleGetListAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetList, requestId, StatusCode.Ok);

        int writeIndexBefore = responseBuffer.WriteIndex;

        // TODO: This as a var uint would be cool, but we save a list copy using the enumerator
        responseBuffer.WriteUInt(0);

        uint count = 0;
        await foreach (ByteString s in this.cache.GetListAsync(key))
        {
            responseBuffer.WriteByteString(s);
            count++;
        }

        int writeIndexAfter = responseBuffer.WriteIndex;
        responseBuffer.WriteIndex = writeIndexBefore;
        responseBuffer.WriteUInt(count);
        responseBuffer.WriteIndex = writeIndexAfter;

        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleAddListAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        ByteString? value = byteBuffer.ReadByteString();
        uint ttlMilliseconds = byteBuffer.ReadVarUInt();
        uint valueTtlMilliseconds = byteBuffer.ReadVarUInt();

        // TODO: Should null values not be allowed in lists?
        if (value is null)
        {
            throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
        }

        await this.cache.AddListAsync(key, value, ttlMilliseconds, valueTtlMilliseconds, ProtocolDefaults.MaxListCount);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateAddListAsync(key, value, ttlMilliseconds, valueTtlMilliseconds);
        }

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.AddList, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleRemoveListAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        ByteString? value = byteBuffer.ReadByteString();

        // TODO: Should null values not be allowed in lists?
        if (value is null)
        {
            throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
        }

        await this.cache.RemoveListAsync(key, value);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateRemoveListAsync(key, value);
        }

        await SendOkResponseAsync(tcpConnection, Operation.RemoveList, requestId, fireAndForget);
    }

    private async ValueTask HandleContainsListAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        // TODO: The internal buffer could be pooled here because it is only used temporarily
        ByteString? value = byteBuffer.ReadByteString();

        // TODO: Should we just return false?
        if (value is null)
        {
            throw new ArgumentException($"Cannot check if null value exist in list '{key}'");
        }

        bool contains = await this.cache.ContainsListAsync(key, value);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.ContainsList, requestId, StatusCode.Ok);
        responseBuffer.WriteBool(contains);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleExistsAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        bool exists = await this.cache.ExistsAsync(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Exists, requestId, StatusCode.Ok);
        responseBuffer.WriteByte(exists ? (byte)1 : (byte)0);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleDeleteAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();

        await this.cache.DeleteAsync(key);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateDeleteAsync(key);
        }

        await SendOkResponseAsync(tcpConnection, Operation.Delete, requestId, fireAndForget);
    }

    private async ValueTask HandleGetObjectBinaryAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        byte[]? value = await this.cache.GetBytesAsync(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetObjectBinary, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt((uint)(value?.Length ?? 0));
        if (value?.Length > 0)
        {
            responseBuffer.WriteBytes(value);
        }
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleSetObjectBinaryAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        uint objectLength = byteBuffer.ReadUInt();
        
        if (objectLength > ProtocolDefaults.MaxByteArrayLength)
        {
            throw new ArgumentException($"Read binary object length '{objectLength}' exceeded max allowed length '{ProtocolDefaults.MaxByteArrayLength}'");
        }

        byte[]? bytes = null;
        if (objectLength > 0)
        {
            bytes = new byte[objectLength];
            byteBuffer.ReadBytes(bytes);
        }

        uint ttlMilliseconds = byteBuffer.ReadVarUInt();

        await this.cache.SetBytesAsync(key, bytes, ttlMilliseconds);

        await SendOkResponseAsync(tcpConnection, Operation.SetObjectBinary, requestId, fireAndForget);
    }

    private ValueTask HandleGetStatsAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetStats, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt((uint)(DateTime.UtcNow - startDateTime).TotalSeconds);
        responseBuffer.WriteULong(this.mimoriaSocketServer.Connections);
        responseBuffer.WriteULong(this.cache.Size);
        responseBuffer.WriteULong(this.cache.Hits);
        responseBuffer.WriteULong(this.cache.Misses);
        responseBuffer.WriteFloat(this.cache.HitRatio);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleGetBytesAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        byte[]? value = await this.cache.GetBytesAsync(key);
        uint valueLength = value is not null ? (uint)value.Length : 0;

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetBytes, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt(valueLength);
        if (valueLength > 0)
        {
            responseBuffer.WriteBytes(value);
        }
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleSetBytesAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        uint valueLength = byteBuffer.ReadVarUInt();
        
        if (valueLength > ProtocolDefaults.MaxByteArrayLength)
        {
            throw new ArgumentException($"Read bytes length '{valueLength}' exceeded max allowed length '{ProtocolDefaults.MaxByteArrayLength}'");
        }

        if (valueLength > 0)
        {
            byte[] value = new byte[valueLength];
            byteBuffer.ReadBytes(value.AsSpan());

            uint ttlMilliseconds = byteBuffer.ReadVarUInt();
            await this.cache.SetBytesAsync(key, value, ttlMilliseconds);

            if (this.replicator is not null)
            {
                await this.replicator.ReplicateSetBytesAsync(key, value, ttlMilliseconds);
            }
        }
        else
        {
            uint ttlMilliseconds = byteBuffer.ReadVarUInt();
            await this.cache.SetBytesAsync(key, bytes: null, ttlMilliseconds);

            if (this.replicator is not null)
            {
                await this.replicator.ReplicateSetBytesAsync(key, value: null, ttlMilliseconds);
            }
        }

        await SendOkResponseAsync(tcpConnection, Operation.SetBytes, requestId, fireAndForget);
    }

    private async ValueTask HandleSetCounterAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        long value = byteBuffer.ReadLong();

        await this.cache.SetCounterAsync(key, value);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateSetCounterAsync(key, value);
        }

        await SendOkResponseAsync(tcpConnection, Operation.SetCounter, requestId, fireAndForget);
    }

    private async ValueTask HandleIncrementCounterAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        long increment = byteBuffer.ReadLong();

        long value = await this.cache.IncrementCounterAsync(key, increment);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateIncrementCounterAsync(key, increment);
        }

        if (fireAndForget)
        {
            return;
        }

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.IncrementCounter, requestId, StatusCode.Ok);
        responseBuffer.WriteLong(value);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleBulkAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        uint operationCount = byteBuffer.ReadVarUInt();

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Bulk, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt(operationCount);

        // TODO: Prime for dictionary, but better default?
        var keyReleasers = new Dictionary<string, ReferenceCountedReleaser?>(capacity: 11, StringComparer.Ordinal);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        async ValueTask LockIfNeededAsync(string key)
        {
            if (!keyReleasers.ContainsKey(key))
            {
                keyReleasers[key] = await this.cache.AutoRemovingAsyncKeyedLocking.LockAsync(key);
            }
        }

        try
        {
            for (int i = 0; i < operationCount; i++)
            {
                var operation = (Operation)byteBuffer.ReadByte();
                switch (operation)
                {
                    case Operation.GetString:
                        {
                            string key = byteBuffer.ReadRequiredKey();

                            await LockIfNeededAsync(key);

                            ByteString? value = await this.cache.GetStringAsync(key, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.GetString);
                            responseBuffer.WriteByteString(value);
                            break;
                        }
                    case Operation.SetString:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            ByteString? value = byteBuffer.ReadByteString();
                            uint ttl = byteBuffer.ReadVarUInt();

                            await LockIfNeededAsync(key);

                            await this.cache.SetStringAsync(key, value, ttl, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.SetString);
                        }
                        break;
                    case Operation.SetObjectBinary:
                        break;
                    case Operation.GetObjectBinary:
                        break;
                    case Operation.GetList:
                        {
                            string key = byteBuffer.ReadRequiredKey();

                            await LockIfNeededAsync(key);

                            responseBuffer.WriteByte((byte)Operation.GetList);

                            int writeIndexBefore = responseBuffer.WriteIndex;

                            // TODO: This as a var uint would be cool, but we save a list copy using the enumerator
                            responseBuffer.WriteUInt(0);

                            uint count = 0;
                            await foreach (ByteString s in this.cache.GetListAsync(key, takeLock: false))
                            {
                                responseBuffer.WriteByteString(s);
                                count++;
                            }

                            int writeIndexAfter = responseBuffer.WriteIndex;
                            responseBuffer.WriteIndex = writeIndexBefore;
                            responseBuffer.WriteUInt(count);
                            responseBuffer.WriteIndex = writeIndexAfter;
                            break;
                        }
                    case Operation.AddList:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            ByteString? value = byteBuffer.ReadByteString();
                            uint ttl = byteBuffer.ReadVarUInt();
                            uint valueTtl = byteBuffer.ReadVarUInt();

                            // TODO: Should null values not be allowed in lists?
                            if (value is null)
                            {
                                throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
                            }

                            await LockIfNeededAsync(key);

                            await this.cache.AddListAsync(key, value, ttl, valueTtl, ProtocolDefaults.MaxListCount, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.AddList);
                            break;
                        }
                    case Operation.RemoveList:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            ByteString? value = byteBuffer.ReadByteString();

                            // TODO: Should null values not be allowed in lists?
                            if (value is null)
                            {
                                throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
                            }

                            await LockIfNeededAsync(key);

                            await this.cache.RemoveListAsync(key, value, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.RemoveList);
                            break;
                        }
                    case Operation.ContainsList:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            // TODO: The internal buffer could be pooled here because it is only used temporarily
                            ByteString? value = byteBuffer.ReadByteString();

                            // TODO: Should we just return false?
                            if (value is null)
                            {
                                throw new ArgumentException($"Cannot check if null value exist in list '{key}'");
                            }

                            await LockIfNeededAsync(key);

                            bool contains = await this.cache.ContainsListAsync(key, value, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.ContainsList);
                            responseBuffer.WriteBool(contains);
                            break;
                        }
                    case Operation.Exists:
                        {
                            string key = byteBuffer.ReadRequiredKey();

                            await LockIfNeededAsync(key);

                            bool exists = await this.cache.ExistsAsync(key, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.Exists);
                            responseBuffer.WriteBool(exists);
                            break;
                        }
                    case Operation.Delete:
                        {
                            string key = byteBuffer.ReadRequiredKey();

                            await LockIfNeededAsync(key);

                            await this.cache.DeleteAsync(key, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.Delete);
                            break;
                        }
                    case Operation.GetBytes:
                        {
                            string key = byteBuffer.ReadRequiredKey();

                            await LockIfNeededAsync(key);

                            byte[]? value = await this.cache.GetBytesAsync(key, takeLock: false);
                            uint valueLength = value is not null ? (uint)value.Length : 0;

                            responseBuffer.WriteByte((byte)Operation.GetBytes);
                            responseBuffer.WriteVarUInt(valueLength);
                            if (valueLength > 0)
                            {
                                responseBuffer.WriteBytes(value);
                            }
                            break;
                        }
                    case Operation.SetBytes:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            uint valueLength = byteBuffer.ReadUInt();

                            if (valueLength > ProtocolDefaults.MaxByteArrayLength)
                            {
                                throw new ArgumentException($"Read bytes length '{valueLength}' exceeded max allowed length '{ProtocolDefaults.MaxByteArrayLength}'");
                            }

                            await LockIfNeededAsync(key);

                            if (valueLength > 0)
                            {
                                var value = new byte[valueLength];
                                byteBuffer.ReadBytes(value.AsSpan());
                                uint ttlMilliseconds = byteBuffer.ReadVarUInt();
                                await this.cache.SetBytesAsync(key, value, ttlMilliseconds, takeLock: false);
                            }
                            else
                            {
                                uint ttlMilliseconds = byteBuffer.ReadVarUInt();
                                await this.cache.SetBytesAsync(key, bytes: null, ttlMilliseconds, takeLock: false);
                            }

                            responseBuffer.WriteByte((byte)Operation.SetBytes);
                            break;
                        }
                    case Operation.SetCounter:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            long value = byteBuffer.ReadLong();

                            await LockIfNeededAsync(key);

                            await this.cache.SetCounterAsync(key, value, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.SetCounter);
                            responseBuffer.EndPacket();
                            break;
                        }
                    case Operation.IncrementCounter:
                        {
                            string key = byteBuffer.ReadRequiredKey();
                            long increment = byteBuffer.ReadLong();

                            await LockIfNeededAsync(key);

                            long value = await this.cache.IncrementCounterAsync(key, increment, takeLock: false);

                            responseBuffer.WriteByte((byte)Operation.IncrementCounter);
                            responseBuffer.WriteLong(value);
                            break;
                        }
                    default:
                        throw new ArgumentException($"Operation '{operation}' cannot be in a bulk operation");
                }
            }
        }
        finally
        {
            foreach (var (_, releaser) in keyReleasers)
            {
                releaser?.Dispose();
            }
        }

        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleGetMapValueAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        string subKey = byteBuffer.ReadString()!;

        MimoriaValue value = await this.cache.GetMapValueAsync(key, subKey);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetMapValue, requestId, StatusCode.Ok);
        responseBuffer.WriteValue(value);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleSetMapValueAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        string subKey = byteBuffer.ReadRequiredKey();
        MimoriaValue value = byteBuffer.ReadValue();
        uint ttlMilliseconds = byteBuffer.ReadVarUInt();

        await this.cache.SetMapValueAsync(key, subKey, value, ttlMilliseconds, ProtocolDefaults.MaxMapCount);

        await SendOkResponseAsync(tcpConnection, Operation.SetMapValue, requestId, fireAndForget);
    }

    private async ValueTask HandleGetMapAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();

        Dictionary<string, MimoriaValue> map = await this.cache.GetMapAsync(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetMap, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt((uint)map.Count);
        foreach (var (subKey, subValue) in map)
        {
            responseBuffer.WriteString(subKey);
            responseBuffer.WriteValue(subValue);
        }
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandleSetMapAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string key = byteBuffer.ReadRequiredKey();
        uint count = byteBuffer.ReadVarUInt();

        if (count > ProtocolDefaults.MaxMapCount)
        {
            throw new ArgumentException($"Read map count '{count}' exceeded max allowed count '{ProtocolDefaults.MaxMapCount}'");
        }

        var map = new Dictionary<string, MimoriaValue>(capacity: (int)count, StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string subKey = byteBuffer.ReadRequiredKey();
            MimoriaValue subValue = byteBuffer.ReadValue();

            map[subKey] = subValue;
        }

        uint ttlMilliseconds = byteBuffer.ReadVarUInt();

        await this.cache.SetMapAsync(key, map, ttlMilliseconds);

        await SendOkResponseAsync(tcpConnection, Operation.SetMap, requestId, fireAndForget);
    }

    private async ValueTask HandleSubscribeAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string channel = byteBuffer.ReadRequiredKey();

        await this.pubSubService.SubscribeAsync(channel, tcpConnection);

        await SendOkResponseAsync(tcpConnection, Operation.Subscribe, requestId, fireAndForget);
    }

    private async ValueTask HandleUnsubscribeAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        string channel = byteBuffer.ReadRequiredKey();

        await this.pubSubService.UnsubscribeAsync(channel, tcpConnection);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Unsubscribe, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask HandlePublishAsync(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer, bool fireAndForget)
    {
        this.metrics.IncrementPubSubMessagesReceived();

        string channel = byteBuffer.ReadRequiredKey();
        MimoriaValue payload = byteBuffer.ReadValue();

        await this.pubSubService.PublishAsync(channel, payload);
    }
}
