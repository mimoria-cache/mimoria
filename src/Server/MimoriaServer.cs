// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Net;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Bully;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Cluster;
using Varelen.Mimoria.Server.Network;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;
using Varelen.Mimoria.Server.PubSub;
using Varelen.Mimoria.Server.Replication;

namespace Varelen.Mimoria.Server;

public sealed class MimoriaServer : IMimoriaServer
{
    private const uint ProtocolVersion = 1;

    private readonly ILogger<MimoriaServer> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly IOptionsMonitor<MimoriaOptions> monitor;
    private readonly IPubSubService pubSubService;
    private readonly IMimoriaSocketServer mimoriaSocketServer;
    private readonly ICache cache;
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
        ICache cache)
    {
        this.logger = logger;
        this.loggerFactory = loggerFactory;
        this.monitor = monitor;
        this.pubSubService = pubSubService;
        this.mimoriaSocketServer = mimoriaSocketServer;
        this.cache = cache;
        this.clusterClients = [];
        this.nodeReadyTaskCompletionSource = new TaskCompletionSource();
        this.clusterReadyTaskCompletionSource = new TaskCompletionSource();

        if (this.monitor.CurrentValue.Cluster is not null)
        {
            this.clusterServer = new ClusterServer(this.loggerFactory.CreateLogger<ClusterServer>(), this.monitor.CurrentValue.Cluster.Ip, this.monitor.CurrentValue.Cluster?.Port ?? 0, this.monitor.CurrentValue.Cluster?.Nodes?.Length ?? 0, this.monitor.CurrentValue.Cluster?.Password!);
            this.clusterServer.AllClientsConnected += HandleAllClientsConnected;
            this.clusterServer.AliveReceived += HandleAliveReceived;
            this.clusterServer.Start();

            this.bullyAlgorithm = new BullyAlgorithm(
                this.loggerFactory.CreateLogger<BullyAlgorithm>(),
                this.monitor.CurrentValue.Cluster!.Id,
                this.monitor.CurrentValue.Cluster!.Nodes.Select(n => n.Id).ToArray(),
                this.clusterServer,
                TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Election.LeaderHeartbeatIntervalMs),
                TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Election.LeaderMissingTimeoutMs),
                TimeSpan.FromMilliseconds(this.monitor.CurrentValue.Cluster.Election.ElectionTimeoutMs));
            this.bullyAlgorithm.LeaderElected += HandleLeaderElected;

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
                var addresses = await Dns.GetHostAddressesAsync(node.Host);

                var clusterClient = new ClusterClient(this.loggerFactory.CreateLogger<ClusterClient>(), this.monitor.CurrentValue.Cluster.Id, addresses[0].ToString(), node.Port, this.bullyAlgorithm!, this.cache, this.monitor.CurrentValue.Cluster.Password!);
                await clusterClient.ConnectAsync();
                
                this.clusterClients.Add(node.Id, clusterClient);
            }

            this.logger.LogInformation("Waiting for node to be ready");
            await this.nodeReadyTaskCompletionSource.Task;
            this.logger.LogInformation("Node ready");

            _ = this.bullyAlgorithm!.StartAsync();

            this.logger.LogInformation("Waiting for cluster to be ready");
            await this.clusterReadyTaskCompletionSource.Task;
            this.logger.LogInformation("Cluster ready");
        }

        this.mimoriaSocketServer.Disconnected += HandleTcpConnectionDisconnected;
        this.mimoriaSocketServer.Start(this.monitor.CurrentValue.Ip, (ushort)this.monitor.CurrentValue.Port, this.monitor.CurrentValue.Backlog);

        this.logger.LogInformation("Mimoria server started on {Ip}:{Port}", this.monitor.CurrentValue.Ip, this.monitor.CurrentValue.Port);
    }

    private void HandleLeaderElected()
    {
        if (!this.clusterReadyTaskCompletionSource.Task.IsCompleted)
        {
            this.clusterReadyTaskCompletionSource.SetResult();
        }
    }

    private void HandleTcpConnectionDisconnected(TcpConnection tcpConnection)
    {
        this.pubSubService.Unsubscribe(tcpConnection);
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
            this.bullyAlgorithm.LeaderElected -= HandleLeaderElected;
            this.bullyAlgorithm.Stop();
        }
        this.mimoriaSocketServer.Disconnected -= HandleTcpConnectionDisconnected;
        this.mimoriaSocketServer.Stop();
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
        var operationHandlers = new Dictionary<Operation, Func<uint, TcpConnection, IByteBuffer, ValueTask>>
        {
            { Operation.Login, this.OnLogin },
            { Operation.GetString, this.OnGetString },
            { Operation.SetString, this.OnSetString },
            { Operation.GetList, this.OnGetList },
            { Operation.AddList, this.OnAddList },
            { Operation.RemoveList, this.OnRemoveList },
            { Operation.ContainsList, this.OnContainsList },
            { Operation.Exists, this.OnExists },
            { Operation.Delete, this.OnDelete },
            { Operation.GetObjectBinary, this.OnGetObjectBinary },
            { Operation.SetObjectBinary, this.OnSetObjectBinary },
            { Operation.GetStats, this.OnGetStats },
            { Operation.GetBytes, this.OnGetBytes },
            { Operation.SetBytes, this.OnSetBytes },
            { Operation.SetCounter, this.OnSetCounter },
            { Operation.IncrementCounter, this.OnIncrementCounter },
            { Operation.Bulk, this.OnBulk },
            { Operation.GetMapValue, this.OnGetMapValue },
            { Operation.SetMapValue, this.OnSetMapValue },
            { Operation.GetMap, this.OnGetMap },
            { Operation.SetMap, this.OnSetMap },
            { Operation.Subscribe, this.OnSubscribe },
            { Operation.Unsubscribe, this.OnUnsubscribe },
            { Operation.Publish, this.OnPublish }
        };

        this.mimoriaSocketServer.SetOperationHandlers(operationHandlers);

        this.logger.LogTrace("Operation handlers registered");
    }

    private ValueTask OnLogin(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
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

        tcpConnection.Authenticated = this.monitor.CurrentValue.Password.Equals(password, StringComparison.Ordinal);

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

    private ValueTask OnGetString(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string? value = this.cache.GetString(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetString, requestId, StatusCode.Ok);
        responseBuffer.WriteString(value);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private async ValueTask OnSetString(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string? value = byteBuffer.ReadString();
        uint ttlMilliseconds = byteBuffer.ReadUInt();

        this.cache.SetString(key, value, ttlMilliseconds);

        if (this.replicator is not null)
        {
            await this.replicator.ReplicateSetStringAsync(key, value, ttlMilliseconds);
        }

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetString, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        await tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnGetList(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        IEnumerable<string> value = this.cache.GetList(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetList, requestId, StatusCode.Ok);

        int writeIndexBefore = responseBuffer.WriteIndex;

        // TODO: This as a var uint would be cool, but we save a list copy using the enumerator
        responseBuffer.WriteUInt(0);

        uint count = 0;
        foreach (string s in value)
        {
            responseBuffer.WriteString(s);
            count++;
        }

        int writeIndexAfter = responseBuffer.WriteIndex;
        responseBuffer.WriteIndex = writeIndexBefore;
        responseBuffer.WriteUInt(count);
        responseBuffer.WriteIndex = writeIndexAfter;

        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnAddList(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string? value = byteBuffer.ReadString();
        uint ttlMilliseconds = byteBuffer.ReadUInt();

        // TODO: Should null values not be allowed in lists?
        if (value is null)
        {
            throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
        }

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetList, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        this.cache.AddList(key, value, ttlMilliseconds);

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnRemoveList(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string? value = byteBuffer.ReadString();

        // TODO: Should null values not be allowed in lists?
        if (value is null)
        {
            throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
        }

        this.cache.RemoveList(key, value);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.RemoveList, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnContainsList(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string? value = byteBuffer.ReadString();

        // TODO: Should we just return false?
        if (value is null)
        {
            throw new ArgumentException($"Cannot check if null value exist in list '{key}'");
        }

        bool contains = this.cache.ContainsList(key, value);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.ContainsList, requestId, StatusCode.Ok);
        responseBuffer.WriteByte(contains ? (byte)1 : (byte)0);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnExists(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Exists, requestId, StatusCode.Ok);
        responseBuffer.WriteByte(this.cache.Exists(key) ? (byte)1 : (byte)0);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnDelete(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;

        this.cache.Delete(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Delete, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnGetObjectBinary(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        byte[]? value = this.cache.GetBytes(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetObjectBinary, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt((uint)(value?.Length ?? 0));
        if (value?.Length > 0)
        {
            responseBuffer.WriteBytes(value);
        }
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnSetObjectBinary(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        uint objectLength = byteBuffer.ReadUInt();
        byte[]? bytes = null;
        if (objectLength > 0)
        {
            bytes = new byte[objectLength];
            byteBuffer.ReadBytes(bytes);
        }
        uint ttlMilliseconds = byteBuffer.ReadVarUInt();

        this.cache.SetBytes(key, bytes, ttlMilliseconds);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetObjectBinary, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnGetStats(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
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

    private ValueTask OnGetBytes(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        byte[]? value = this.cache.GetBytes(key);
        uint valueLength = value is not null ? (uint)value.Length : 0;

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetBytes, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt(valueLength);
        if (valueLength > 0)
        {
            responseBuffer.WriteBytes(value);
        }
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnSetBytes(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        uint valueLength = byteBuffer.ReadVarUInt();

        if (valueLength > 0)
        {
            byte[] value = new byte[valueLength];
            byteBuffer.ReadBytes(value.AsSpan());

            uint ttlMilliseconds = byteBuffer.ReadUInt();
            this.cache.SetBytes(key, value, ttlMilliseconds);
        }
        else
        {
            uint ttlMilliseconds = byteBuffer.ReadUInt();
            this.cache.SetBytes(key, null, ttlMilliseconds);
        }

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetBytes, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnSetCounter(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        long value = byteBuffer.ReadLong();

        this.cache.SetCounter(key, value);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetCounter, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnIncrementCounter(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        long increment = byteBuffer.ReadLong();

        long value = this.cache.IncrementCounter(key, increment);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.IncrementCounter, requestId, StatusCode.Ok);
        responseBuffer.WriteLong(value);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnBulk(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        uint operationCount = byteBuffer.ReadVarUInt();

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Bulk, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt(operationCount);

        for (int i = 0; i < operationCount; i++)
        {
            var operation = (Operation)byteBuffer.ReadByte();
            switch (operation)
            {
                case Operation.Login:
                    break;
                case Operation.GetString:
                    {
                        string key = byteBuffer.ReadString()!;
                        string? value = this.cache.GetString(key);

                        responseBuffer.WriteByte((byte)Operation.GetString);
                        responseBuffer.WriteString(value);
                        break;
                    }
                case Operation.SetString:
                    {
                        string key = byteBuffer.ReadString()!;
                        string? value = byteBuffer.ReadString();
                        uint ttl = byteBuffer.ReadUInt();

                        this.cache.SetString(key, value, ttl);

                        responseBuffer.WriteByte((byte)Operation.SetString);
                    }
                    break;
                case Operation.SetObjectBinary:
                    break;
                case Operation.GetObjectBinary:
                    break;
                case Operation.GetList:
                    break;
                case Operation.AddList:
                    break;
                case Operation.RemoveList:
                    break;
                case Operation.ContainsList:
                    break;
                case Operation.Exists:
                    {
                        string key = byteBuffer.ReadString()!;

                        bool exists = this.cache.Exists(key);

                        responseBuffer.WriteByte((byte)Operation.Exists);
                        responseBuffer.WriteBool(exists);
                        break;
                    }
                case Operation.Delete:
                    {
                        string key = byteBuffer.ReadString()!;

                        this.cache.Delete(key);

                        responseBuffer.WriteByte((byte)Operation.Delete);
                        break;
                    }
                case Operation.GetStats:
                    break;
                case Operation.GetBytes:
                    break;
                case Operation.SetBytes:
                    break;
                case Operation.SetCounter:
                    break;
                case Operation.IncrementCounter:
                    break;
                case Operation.Bulk:
                    break;
                default:
                    break;
            }
        }

        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnGetMapValue(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string subKey = byteBuffer.ReadString()!;

        MimoriaValue value = this.cache.GetMapValue(key, subKey);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetMapValue, requestId, StatusCode.Ok);
        responseBuffer.WriteValue(value);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnSetMapValue(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string subKey = byteBuffer.ReadString()!;
        MimoriaValue value = byteBuffer.ReadValue();
        uint ttlMilliseconds = byteBuffer.ReadUInt();

        this.cache.SetMapValue(key, subKey, value, ttlMilliseconds);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetMapValue, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnGetMap(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;

        Dictionary<string, MimoriaValue> map = this.cache.GetMap(key);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetMap, requestId, StatusCode.Ok);
        responseBuffer.WriteVarUInt((uint)map.Count);
        foreach (var (subKey, subValue) in map)
        {
            responseBuffer.WriteString(subKey);
            responseBuffer.WriteValue(subValue);
        }
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnSetMap(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        uint count = byteBuffer.ReadVarUInt();

        var map = new Dictionary<string, MimoriaValue>(capacity: (int)count);
        for (int i = 0; i < count; i++)
        {
            string subKey = byteBuffer.ReadString()!;
            MimoriaValue subValue = byteBuffer.ReadValue();

            map[subKey] = subValue;
        }

        uint ttlMilliseconds = byteBuffer.ReadUInt();

        this.cache.SetMap(key, map, ttlMilliseconds);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetMap, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnSubscribe(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string channel = byteBuffer.ReadString()!;

        this.pubSubService.Subscribe(channel, tcpConnection);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Subscribe, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnUnsubscribe(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string channel = byteBuffer.ReadString()!;

        this.pubSubService.Unsubscribe(channel, tcpConnection);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Unsubscribe, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
    }

    private ValueTask OnPublish(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string channel = byteBuffer.ReadString()!;
        MimoriaValue payload = byteBuffer.ReadValue();

        return this.pubSubService.PublishAsync(channel, payload);
    }
}
