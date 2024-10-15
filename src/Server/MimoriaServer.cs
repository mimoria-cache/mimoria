// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Network;
using Varelen.Mimoria.Server.Options;
using Varelen.Mimoria.Server.Protocol;

namespace Varelen.Mimoria.Server;

public sealed class MimoriaServer : IMimoriaServer
{
    private const uint ProtocolVersion = 1;

    private readonly ILogger<MimoriaServer> logger;
    private readonly IOptionsMonitor<ServerOptions> monitor;
    private readonly IServerIdProvider serverIdProvider;
    private readonly IMimoriaSocketServer mimoriaSocketServer;
    private readonly ICache cache;

    private Guid serverId;
    private DateTime startDateTime;

    public MimoriaServer(
        ILogger<MimoriaServer> logger,
        IOptionsMonitor<ServerOptions> monitor,
        IServerIdProvider serverIdProvider,
        IMimoriaSocketServer mimoriaSocketServer,
        ICache cache)
    {
        this.logger = logger;
        this.monitor = monitor;
        this.serverIdProvider = serverIdProvider;
        this.mimoriaSocketServer = mimoriaSocketServer;
        this.cache = cache;
    }

    public void Start()
    {
        this.serverId = this.serverIdProvider.GetServerId();
        this.monitor.OnChange(OnOptionsChanged);

        this.RegisterOperationHandlers();

        this.mimoriaSocketServer.Start(this.monitor.CurrentValue.Ip, this.monitor.CurrentValue.Port, this.monitor.CurrentValue.Backlog);
        this.startDateTime = DateTime.UtcNow;
        this.logger.LogInformation("Mimoria server started on {Ip}:{Port}", this.monitor.CurrentValue.Ip, this.monitor.CurrentValue.Port);
    }

    public void Stop()
    {
        this.mimoriaSocketServer.Stop();
        this.logger.LogInformation("Mimoria server stopped");
    }

    private void OnOptionsChanged(ServerOptions cacheServerOptions)
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
            { Operation.SetMap, this.OnSetMap }
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
            responseBuffer.WriteGuid(this.serverId);
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

    private ValueTask OnSetString(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string key = byteBuffer.ReadString()!;
        string? value = byteBuffer.ReadString();
        uint ttlMilliseconds = byteBuffer.ReadUInt();

        this.cache.SetString(key, value, ttlMilliseconds);

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.SetString, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        return tcpConnection.SendAsync(responseBuffer);
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
                    break;
                case Operation.Delete:
                    break;
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
}
