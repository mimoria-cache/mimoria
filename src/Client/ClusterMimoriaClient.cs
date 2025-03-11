// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public sealed class ClusterMimoriaClient : IClusterMimoriaClient
{
    private readonly List<IMimoriaClient> mimoriaClients;
    private readonly string password;
    private readonly IPEndPoint[] ipEndPoints;

    public int? ServerId => throw new NotSupportedException();

    public bool IsConnected => throw new NotSupportedException();

    public bool IsPrimary => throw new NotSupportedException();

    public ClusterMimoriaClient(string password, params IPEndPoint[] ipEndPoints)
    {
        this.mimoriaClients = new List<IMimoriaClient>();
        this.password = password;
        this.ipEndPoints = ipEndPoints;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IMimoriaClient GetPrimary()
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            if (mimoriaClient.IsPrimary && mimoriaClient.IsConnected)
            {
                return mimoriaClient;
            }
        }

        throw new InvalidOperationException("No primary available");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IMimoriaClient GetReadingClient(bool preferSecondary)
    {
        IMimoriaClient? readingMimoriaClient = null;
        int count = 0;

        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            if (!mimoriaClient.IsConnected)
            {
                continue;
            }

            if (preferSecondary && mimoriaClient.IsPrimary)
            {
                continue;
            }

            count++;

            if (Random.Shared.Next(count) == 0)
            {
                readingMimoriaClient = mimoriaClient;
            }
        }

        return readingMimoriaClient ?? throw new InvalidOperationException("No client for reading available");
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IPEndPoint remoteEndPoint in this.ipEndPoints)
        {
            var mimoriaClient = new MimoriaClient(remoteEndPoint.Address.ToString(), (ushort)remoteEndPoint.Port, this.password);
 
            await mimoriaClient.ConnectAsync(cancellationToken);

            this.mimoriaClients.Add(mimoriaClient);
        }
    }

    public async Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        try
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();

            await mimoriaClient.SetStringAsync(key, value, ttl, cancellationToken);
        }
        catch (TimeoutException)
        {
            // TODO: Retry?
        }
    }

    public Task<string?> GetStringAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetStringAsync(key, cancellationToken);
    }

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        => this.GetStringAsync(key, preferSecondary: false, cancellationToken);

    public async Task AddListAsync(string key, string value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        try
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();

            await mimoriaClient.AddListAsync(key, value, ttl, cancellationToken);
        }
        catch (TimeoutException)
        {
            // TODO: Retry?
        }
    }

    public IBulkOperation Bulk()
    {
        IMimoriaClient mimoriaClient = this.GetPrimary();
        return new BulkOperation((MimoriaClient)mimoriaClient);
    }

    public Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
        => this.ContainsListAsync(key, value, preferSecondary: false, cancellationToken);

    public Task<bool> ContainsListAsync(string key, string value, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.ContainsListAsync(key, value, cancellationToken);
    }

    public async ValueTask<long> DecrementCounterAsync(string key, long decrement, CancellationToken cancellationToken = default)
    {
        try
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();

            return await mimoriaClient.DecrementCounterAsync(key, decrement, cancellationToken);
        }
        catch (TimeoutException)
        {
            // TODO: Retry?
            return 0;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();

            await mimoriaClient.DeleteAsync(key, cancellationToken);
        }
        catch (TimeoutException)
        {
            // TODO: Retry?
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            await mimoriaClient.DisconnectAsync(cancellationToken);
        }
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => this.ExistsAsync(key, preferSecondary: false, cancellationToken);

    public ValueTask<bool> ExistsAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.ExistsAsync(key, cancellationToken);
    }

    public Task<byte[]?> GetBytesAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetBytesAsync(key, cancellationToken);
    }

    public Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
        => this.GetBytesAsync(key, preferSecondary: false, cancellationToken);

    public Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
        => this.GetListAsync(key, preferSecondary: false, cancellationToken);

    public Task<List<string>> GetListAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetListAsync(key, cancellationToken);
    }

    public IAsyncEnumerable<string> GetListEnumerableAsync(string key, CancellationToken cancellationToken = default)
        => this.GetListEnumerableAsync(key, preferSecondary: false, cancellationToken);

    public IAsyncEnumerable<string> GetListEnumerableAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetListEnumerableAsync(key, cancellationToken);
    }

    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
        => this.GetMapAsync(key, preferSecondary: false, cancellationToken);

    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetMapAsync(key, cancellationToken);
    }

    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetMapValueAsync(key, subKey, cancellationToken);
    }

    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default)
        => this.GetMapValueAsync(key, subKey, preferSecondary: false, cancellationToken);

    public Task<T?> GetObjectBinaryAsync<T>(string key, bool preferSecondary, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
    }

    public Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
        => this.GetObjectBinaryAsync<T>(key, preferSecondary: false, cancellationToken);

    public Task<T?> GetObjectJsonAsync<T>(string key, bool preferSecondary, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
    }

    public Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        => this.GetObjectJsonAsync<T>(key, preferSecondary: false, jsonSerializerOptions, cancellationToken);

    public Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetPrimary();
        return mimoriaClient.GetStatsAsync(cancellationToken);
    }

    public async ValueTask<long> IncrementCounterAsync(string key, long increment, CancellationToken cancellationToken = default)
    {
        try
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();

            return await mimoriaClient.IncrementCounterAsync(key, increment, cancellationToken);
        }
        catch (TimeoutException)
        {
            // TODO: Retry?
            return -1;
        }
    }

    public ValueTask<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, cancellationToken);

    public Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetPrimary();
        return mimoriaClient.PublishAsync(channel, payload, cancellationToken);
    }

    public Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetCounterAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        try
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();

            await mimoriaClient.SetMapAsync(key, map, ttl, cancellationToken);
        }
        catch (TimeoutException)
        {
            // TODO: Retry?
        }
    }

    public Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetPrimary();
        return mimoriaClient.SubscribeAsync(channel, cancellationToken);
    }

    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetPrimary();
        return mimoriaClient.UnsubscribeAsync(channel, cancellationToken);
    }

    public async ValueTask DisposeAsync()
        => await this.DisconnectAsync();
}
