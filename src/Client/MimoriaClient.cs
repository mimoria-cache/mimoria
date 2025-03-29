// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Client.Network;
using Varelen.Mimoria.Client.Protocol;
using Varelen.Mimoria.Client.Retry;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

namespace Varelen.Mimoria.Client;

/// <summary>
/// An implementation of a Mimoria client that communicates with a Mimoria server using sockets.
/// </summary>
public sealed class MimoriaClient : IMimoriaClient
{
    private const uint ProtocolVersion = 1;

    private readonly IMimoriaSocketClient mimoriaSocketClient;
    private readonly IRetryPolicy connectRetryPolicy;

    private uint requestIdCounter;

    private readonly string ip;
    private readonly ushort port;
    private readonly string password;

    private bool isPrimary;

    private volatile bool connecting;

    /// <inheritdoc />
    public int? ServerId { get; private set; }

    /// <inheritdoc />
    public bool IsConnected => this.mimoriaSocketClient.IsConnected;

    /// <inheritdoc />
    public bool IsPrimary
    {
        get => this.isPrimary;
        set => this.isPrimary = value;
    }

    /// <summary>
    /// Creates a new Mimoria client with the specified IP endpoint and password.
    /// </summary>
    public MimoriaClient(IPEndPoint ipEndPoint, string password = "")
        : this(ipEndPoint.Address.ToString(), (ushort)ipEndPoint.Port, password)
    {

    }

    /// <summary>
    /// Creates a new Mimoria client with the specified IP address, port, and password.
    /// </summary>
    public MimoriaClient(string ip, ushort port, string password = "")
        : this(ip, port, password, new MimoriaSocketClient())
    {

    }

    /// <summary>
    /// Creates a new Mimoria client with the specified IP address, port, password, and Mimoria socket client.
    /// </summary>
    public MimoriaClient(string ip, ushort port, string password, IMimoriaSocketClient mimoriaSocketClient)
        : this(ip, port, password, mimoriaSocketClient, new LinearRetryPolicy(initialDelay: 250, maxRetries: 255, typeof(SocketException)))
    {

    }

    /// <summary>
    /// Creates a new Mimoria client with the specified IP address, port, password, Mimoria socket client, and connect retry policy.
    /// </summary>
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
        this.isPrimary = false;
    }

    private void HandleDisconnected()
    {
        if (this.connecting)
        {
            return;
        }

        _ = Task.Run(async () => await this.ConnectAsync());
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.mimoriaSocketClient.IsConnected || this.connecting)
        {
            return;
        }

        if (this.password.Length > ProtocolDefaults.MaxPasswordLength)
        {
            throw new ArgumentException($"Password can only be '{ProtocolDefaults.MaxPasswordLength}' characters long but was '{this.password.Length}'");
        }

        this.connecting = true;

        await this.connectRetryPolicy.ExecuteAsync(async () =>
        {
            await this.mimoriaSocketClient.ConnectAsync(this.ip, this.port, cancellationToken);

            this.connecting = false;

            uint requestId = this.GetNextRequestId();

            IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Login, requestId);
            byteBuffer.WriteVarUInt(ProtocolVersion);
            byteBuffer.WriteString(this.password);
            byteBuffer.EndPacket();

            using IByteBuffer response =
                await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
            if (response.ReadByte() != 1)
            {
                throw new MimoriaConnectionException($"Login to Mimoria instance at '{this.ip}:{this.port}' failed. Given password did not match configured password");
            }
            this.ServerId = response.ReadInt();
            this.isPrimary = response.ReadBool();

            await this.ResubscribeIfNeeded();
        }, cancellationToken);
    }

    private async ValueTask ResubscribeIfNeeded()
    {
        var subscribedChannels = this.mimoriaSocketClient.SubscribedChannels;
        if (subscribedChannels.Count == 0)
        {
            return;
        }

        foreach (var channel in subscribedChannels)
        {
            await this.SendSubscribeAsync(channel);
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        => await this.mimoriaSocketClient.DisconnectAsync(force: true, cancellationToken);

    /// <inheritdoc />
    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetString, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadString();
    }

    /// <inheritdoc />
    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetString, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(value);
        byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetList, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        uint count = response.ReadUInt();
        if (count == 0)
        {
            yield break;
        }

        for (uint i = 0; i < count; i++)
        {
            yield return response.ReadString()!;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetList, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        uint count = response.ReadUInt();
        if (count == 0)
        {
            return new List<string>();
        }

        var list = new List<string>(capacity: (int)count);
        for (uint i = 0; i < count; i++)
        {
            list.Add(response.ReadString()!);
        }
        return list;
    }

    /// <inheritdoc />
    public async Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.AddList, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(value);
        byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.WriteVarUInt((uint)valueTtl.TotalMilliseconds);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.RemoveList, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(value);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.ContainsList, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(value);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadByte() == 1;
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetObjectBinary, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);

        uint objectLength = response.ReadVarUInt();
        if (objectLength == 0)
        {
            return default;
        }

        var typeInstance = Activator.CreateInstance<T>();
        typeInstance.Deserialize(response);
        return typeInstance;
    }

    /// <inheritdoc />
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

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        string? json = await this.GetStringAsync(key, cancellationToken);
        if (json is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, jsonSerializerOptions);
    }

    /// <inheritdoc />
    public async Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        string? json = t != null
            ? JsonSerializer.Serialize<T>(t, jsonSerializerOptions)
            : null;

        await this.SetStringAsync(key, json, ttl, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetBytes, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        uint size = response.ReadVarUInt();
        if (size == 0)
        {
            return null;
        }

        // TODO: How can we pool this? Problem is if we use the shared pool the caller needs to return it by hand to the pool after using it..
        byte[] bytes = new byte[size];
        response.ReadBytes(bytes.AsSpan());
        return bytes;
    }

    /// <inheritdoc />
    public async Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();
        uint valueLength = value is not null ? (uint)value.Length : 0;

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetBytes, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteVarUInt(valueLength);
        if (valueLength > 0)
        {
            byteBuffer.WriteBytes(value.AsSpan());
        }
        byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetCounterAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetCounter, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteLong(value);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, long increment = 1, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.IncrementCounter, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteLong(increment);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadLong();
    }

    /// <inheritdoc />
    public Task<long> DecrementCounterAsync(string key, long decrement, CancellationToken cancellationToken = default)
    {
        return IncrementCounterAsync(key, -decrement, cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Exists, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadByte() == 1;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Delete, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetStats, requestId);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
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

    /// <inheritdoc />
    public async Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetMapValue, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(subKey);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        return response.ReadValue();
    }

    /// <inheritdoc />
    public async Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetMapValue, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteString(subKey);
        byteBuffer.WriteValue(subValue);
        byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.GetMap, requestId);
        byteBuffer.WriteString(key);

        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);

        uint count = response.ReadVarUInt();

        var map = new Dictionary<string, MimoriaValue>(capacity: (int)count);
        for (int i = 0; i < count; i++)
        {
            string subKey = response.ReadString()!;
            MimoriaValue subValue = response.ReadValue();

            map[subKey] = subValue;
        }

        return map;
    }

    /// <inheritdoc />
    public async Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.SetMap, requestId);
        byteBuffer.WriteString(key);
        byteBuffer.WriteVarUInt((uint)map.Count);
        foreach (var (subKey, subValue) in map)
        {
            byteBuffer.WriteString(subKey);
            byteBuffer.WriteValue(subValue);
        }
        byteBuffer.WriteVarUInt((uint)ttl.TotalMilliseconds);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public IBulkOperation Bulk()
        => new BulkOperation(this);

    /// <inheritdoc />
    public async Task<List<object?>> ExecuteBulkAsync(BulkOperation bulkOperation, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Bulk, requestId);
        byteBuffer.WriteVarUInt(bulkOperation.OperationCount);
        byteBuffer.WriteBytes(bulkOperation.ByteBuffer.Bytes.AsSpan(0, bulkOperation.ByteBuffer.Size));
        byteBuffer.EndPacket();

        bulkOperation.Dispose();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
        uint operationCount = response.ReadVarUInt();

        var list = new List<object?>(capacity: (int)operationCount);

        for (uint i = 0; i < operationCount; i++)
        {
            var operation = (Operation)response.ReadByte();
            switch (operation)
            {
                case Operation.Login:
                    break;
                case Operation.GetString:
                    {
                        string? value = response.ReadString();
                        list.Add(value);
                        break;
                    }
                case Operation.SetString:
                    // Nothing to do
                    list.Add(true);
                    break;
                case Operation.SetObjectBinary:
                    break;
                case Operation.GetObjectBinary:
                    break;
                case Operation.GetList:
                    {
                        uint count = response.ReadUInt();
                        if (count == 0)
                        {
                            list.Add(new List<string>());
                            break;
                        }

                        var listValues = new List<string>(capacity: (int)count);
                        for (uint j = 0; j < count; j++)
                        {
                            listValues.Add(response.ReadString()!);
                        }

                        list.Add(listValues);
                        break;
                    }
                case Operation.AddList:
                    {
                        // Nothing to do
                        list.Add(true);
                        break;
                    }
                case Operation.RemoveList:
                    {
                        // Nothing to do
                        list.Add(true);
                        break;
                    }
                case Operation.ContainsList:
                    {
                        bool exists = response.ReadBool();
                        list.Add(exists);
                        break;
                    }
                case Operation.Exists:
                    {
                        bool exists = response.ReadBool();
                        list.Add(exists);
                        break;
                    }
                case Operation.Delete:
                    // Nothing to do
                    list.Add(true);
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
                    {
                        long value = response.ReadLong();
                        list.Add(value);
                        break;
                    }
                case Operation.Bulk:
                    break;
                default:
                    break;
            }
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        (Subscription subscription, bool alreadySubscribed) = this.mimoriaSocketClient.Subscribe(channel);

        if (!alreadySubscribed)
        {
            await this.SendSubscribeAsync(channel, cancellationToken);
        }

        return subscription;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task SendSubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Subscribe, requestId);
        byteBuffer.WriteString(channel);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        if (!this.mimoriaSocketClient.Unsubscribe(channel))
        {
            // We have no subscription, so do nothing
            return;
        }

        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Unsubscribe, requestId);
        byteBuffer.WriteString(channel);
        byteBuffer.EndPacket();

        using IByteBuffer response = await this.mimoriaSocketClient.SendAndWaitForResponseAsync(requestId, byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        uint requestId = this.GetNextRequestId();

        IByteBuffer byteBuffer = PooledByteBuffer.FromPool(Operation.Publish, requestId);
        byteBuffer.WriteString(channel);
        byteBuffer.WriteValue(payload);
        byteBuffer.EndPacket();

        await this.mimoriaSocketClient.SendAndForgetAsync(byteBuffer, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
        => this.mimoriaSocketClient.DisposeAsync();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetNextRequestId()
        => Interlocked.Increment(ref this.requestIdCounter);
}
