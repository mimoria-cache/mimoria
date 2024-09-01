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
        this.logger.LogInformation("Mimoria server started on {ip}:{port}", this.monitor.CurrentValue.Ip, this.monitor.CurrentValue.Port);
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
        this.mimoriaSocketServer.SetOperationHandler(Operation.Login, this.OnLogin);
        this.mimoriaSocketServer.SetOperationHandler(Operation.GetString, this.OnGetString);
        this.mimoriaSocketServer.SetOperationHandler(Operation.SetString, this.OnSetString);
        this.mimoriaSocketServer.SetOperationHandler(Operation.GetList, this.OnGetList);
        this.mimoriaSocketServer.SetOperationHandler(Operation.AddList, this.OnAddList);
        this.mimoriaSocketServer.SetOperationHandler(Operation.RemoveList, this.OnRemoveList);
        this.mimoriaSocketServer.SetOperationHandler(Operation.ContainsList, this.OnContainsList);
        this.mimoriaSocketServer.SetOperationHandler(Operation.Exists, this.OnExists);
        this.mimoriaSocketServer.SetOperationHandler(Operation.Delete, this.OnDelete);
        this.mimoriaSocketServer.SetOperationHandler(Operation.GetObjectBinary, this.OnGetObjectBinary);
        this.mimoriaSocketServer.SetOperationHandler(Operation.SetObjectBinary, this.OnSetObjectBinary);
        this.mimoriaSocketServer.SetOperationHandler(Operation.GetStats, this.OnGetStats);

        this.logger.LogTrace("Operation handlers registered");
    }

    private ValueTask OnLogin(uint requestId, TcpConnection tcpConnection, IByteBuffer byteBuffer)
    {
        string password = byteBuffer.ReadString()!;

        tcpConnection.Authenticated = this.monitor.CurrentValue.Password == password;

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.Login, requestId, StatusCode.Ok);
        responseBuffer.WriteByte(tcpConnection.Authenticated ? (byte)1 : (byte)0);
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

        IByteBuffer responseBuffer = PooledByteBuffer.FromPool(Operation.GetList, requestId, StatusCode.Ok);
        responseBuffer.EndPacket();

        // TODO: Should null values not be allowed in lists?
        if (value is null)
        {
            responseBuffer.Dispose();
            throw new ArgumentException($"Cannot remove null value from list under key '{key}'");
        }

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
}
