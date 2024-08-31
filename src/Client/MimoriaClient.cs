// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Client.Retry;
using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Client.Protocol;
using Varelen.Mimoria.Client.Network;

namespace Varelen.Mimoria.Client;

public sealed class MimoriaClient : IMimoriaClient
{
    private const byte MaxPasswordLength = 24;

    private readonly IMimoriaSocketClient mimoriaSocketClient;
    private readonly IRetryPolicy connectRetryPolicy;

    private uint requestIdCounter;

    private readonly string ip;
    private readonly ushort port;
    private readonly string password;

    private volatile bool connecting;

    public Guid? ServerId { get; private set; }

    public MimoriaClient(string ip, ushort port, string password = "")
        : this(ip, port, password, null)
    {

    }

    public MimoriaClient(string ip, ushort port, string password, IMimoriaSocketClient mimoriaSocketClient)
        : this(ip, port, password, mimoriaSocketClient, new ExponentialRetryPolicy(250, 4, typeof(SocketException)))
    {

    }

    public MimoriaClient(
        string ip,
        ushort port,
        string password,
        IMimoriaSocketClient mimoriaSocketClient,
        IRetryPolicy connectRetryPolicy)
    {
        this.ip = ip;
        this.port = port;
        this.password = password;
        this.mimoriaSocketClient = mimoriaSocketClient;
        this.mimoriaSocketClient.Disconnected += HandleDisconnected;
        this.connectRetryPolicy = connectRetryPolicy;
    }

    private void HandleDisconnected()
    {
        if (this.connecting)
        {
            return;
        }

        Task.Run(() => this.ConnectAsync());
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.mimoriaSocketClient.IsConnected || connecting)
        {
            return;
        }

        this.connecting = true;

        await this.connectRetryPolicy.ExecuteAsync(async () =>
        {
            if (password.Length > MaxPasswordLength)
            {
                throw new ArgumentException($"Password can only be {MaxPasswordLength} characters long");
            }

            await this.mimoriaSocketClient.ConnectAsync(ip, port, cancellationToken);

            this.connecting = false;

            uint requestId = this.GetNextRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Login, requestId);
            byteBuffer.WriteString(password);
            byteBuffer.EndPacket();

            using IByteBuffer response =
                await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
            if (response.ReadByte() != 1)
            {
                throw new MimoriaConnectionException($"Login to Mimoria instance at {ip}:{port} failed. Given password did not match configured password");
            }
            this.ServerId = response.ReadGuid();
            return true;
        }, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        this.connecting = true;
        await this.mimoriaSocketClient.DisconnectAsync(cancellationToken);
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetString, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadString();
    }

    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetString, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(value);
        byteBuffer.WriteUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    public Task<IList<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task AddListAsync(string key, string value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetObjectBinary, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);

        uint objectLength = response.ReadVarUInt();
        if (objectLength == 0)
        {
            return default;
        }

        var typeInstance = Activator.CreateInstance<T>();
        typeInstance.Deserialize(response);
        return typeInstance;
    }

    public async Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetObjectBinary, requestId);
        byteBuffer.WriteString(key);
        // Object length marker which is written after the object was written
        byteBuffer.WriteUInt(0);
        if (binarySerializable != null)
        {
            int writeIndexBefore = byteBuffer.WriteIndex;
            binarySerializable.Serialize(byteBuffer);
            int writeIndexAfter = byteBuffer.WriteIndex;
            int sizeAfter = writeIndexAfter - writeIndexBefore;
            byteBuffer.WriteIndex = writeIndexBefore - 4;
            byteBuffer.WriteUInt((uint)sizeAfter);
            byteBuffer.WriteIndex = writeIndexAfter;
        }

        byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.EndPacket();

        await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    public async Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default) where T : new()
    {
        string? json = await this.GetStringAsync(key, cancellationToken);
        if (json is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
    }

    public async Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default) where T : new()
    {
        string? json = t != null
            ? JsonSerializer.Serialize<T>(t, jsonSerializerOptions)
            : null;

        await this.SetStringAsync(key, json, ttl, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Exists, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadByte() == 1;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Delete, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetNextRequestId()
        => Interlocked.Increment(ref this.requestIdCounter);

    public async Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetStats, requestId);
        byteBuffer.EndPacket();

        IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return new Stats
        {
            Uptime = response.ReadVarUInt(),
            Connections = response.ReadULong(),
            CacheSize = response.ReadULong(),
            CacheHits = response.ReadULong(),
            CacheMisses = response.ReadULong(),
            CacheHitRatio = response.ReadFloat()
        };
    }
}
