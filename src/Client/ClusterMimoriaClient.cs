// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Varelen.Mimoria.Client.Exceptions;
using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Client;

public sealed class ClusterMimoriaClient : IClusterMimoriaClient
{
    private const int DefaultPrimaryRetryCount = 6;
    private const int DefaultPrimaryRetryDelay = 1_000;

    private readonly List<IMimoriaClient> mimoriaClients;
    private readonly string password;
    private readonly IPEndPoint[] ipEndPoints;
    private readonly int primaryRetryCount;
    private readonly int primaryRetryDelay;

    public int? ServerId => this.mimoriaClients.Where(mimoriaClient => mimoriaClient.IsPrimary).First().ServerId;

    public bool IsConnected => this.mimoriaClients.All(mimoriaClient => mimoriaClient.IsConnected);

    public bool IsPrimary { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.mimoriaClients;

    public ClusterMimoriaClient(string password, params IPEndPoint[] ipEndPoints)
        : this(password, DefaultPrimaryRetryCount, DefaultPrimaryRetryDelay, ipEndPoints)
    {

    }

    public ClusterMimoriaClient(string password, int primaryRetryCount, int primaryRetryDelay, params IPEndPoint[] ipEndPoints)
    {
        this.password = password;
        this.primaryRetryCount = primaryRetryCount;
        this.primaryRetryDelay = primaryRetryDelay;
        this.ipEndPoints = ipEndPoints;
        this.mimoriaClients = new List<IMimoriaClient>();
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

        throw new NoPrimaryAvailableException("No primary available");
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

        return readingMimoriaClient ?? throw new NoSecondaryAvailableException();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IPEndPoint remoteEndPoint in this.ipEndPoints)
        {
            var mimoriaClient = new MimoriaClient(remoteEndPoint, this.password);
 
            await mimoriaClient.ConnectAsync(cancellationToken);

            var subscription = await mimoriaClient.SubscribeAsync(Channels.PrimaryChanged, cancellationToken);
            subscription.Payload += HandlePrimaryChanged;

            this.mimoriaClients.Add(mimoriaClient);
        }
    }

    private void HandlePrimaryChanged(MimoriaValue payload)
    {
        int newPrimaryServerId = payload;

        var oldPrimary = this.mimoriaClients.FirstOrDefault(mimoriaClient => mimoriaClient.IsPrimary);

        Debug.Assert(oldPrimary is not null, "Old primary not found in mimoria clients list");
        Debug.Assert(oldPrimary.IsPrimary, "Old primary is not marked as primary");

        if (oldPrimary is not null)
        {
            oldPrimary.IsPrimary = false;
        }

        var newPrimary = this.mimoriaClients.FirstOrDefault(mimoriaClient => mimoriaClient.ServerId == newPrimaryServerId);

        Debug.Assert(newPrimary is not null, "New primary not found in mimoria clients list");
        Debug.Assert(!newPrimary.IsPrimary, "New primary is already marked as primary");

        if (newPrimary is not null)
        {
            newPrimary.IsPrimary = true;
        }
    }

    private async Task RetryPrimaryOperationAsync(Func<Task> operationAsync, int retry = 1)
    {
        if (retry > this.primaryRetryCount)
        {
            throw new NoPrimaryAvailableException("No primary was available even after extended retrying");
        }

        try
        {
            await operationAsync();
        }
        catch (Exception exception) when (exception is TimeoutException or NoPrimaryAvailableException)
        {
            await Task.Delay(this.primaryRetryDelay);

            await RetryPrimaryOperationAsync(operationAsync, retry + 1);
        }
    }

    private async Task<T> RetryPrimaryOperationAsync<T>(Func<Task<T>> operationAsync, int retry = 1)
    {
        if (retry > this.primaryRetryCount)
        {
            throw new NoPrimaryAvailableException("No primary was available even after extended retrying");
        }

        try
        {
            return await operationAsync();
        }
        catch (Exception exception) when (exception is TimeoutException or NoPrimaryAvailableException)
        {
            await Task.Delay(this.primaryRetryDelay);

            return await RetryPrimaryOperationAsync(operationAsync, retry + 1);
        }
    }

    public Task SetStringAsync(string key, string? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetStringAsync(key, value, ttl, cancellationToken);
        });
    }

    public Task<string?> GetStringAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
        return mimoriaClient.GetStringAsync(key, cancellationToken);
    }

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        => this.GetStringAsync(key, preferSecondary: false, cancellationToken);

    public Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.AddListAsync(key, value, ttl, valueTtl, cancellationToken);
        });
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

    public Task<long> DecrementCounterAsync(string key, long decrement, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.DecrementCounterAsync(key, decrement, cancellationToken);
        });
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.DeleteAsync(key, cancellationToken);
        });
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
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.GetStatsAsync(cancellationToken);
        });
    }

    public async Task<long> IncrementCounterAsync(string key, long increment, CancellationToken cancellationToken = default)
    {
        return await RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.IncrementCounterAsync(key, increment, cancellationToken);
        });
    }

    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, cancellationToken);

    public Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.PublishAsync(channel, payload, cancellationToken);
        });
    }

    public Task RemoveListAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.RemoveListAsync(key, value, cancellationToken);
        });
    }

    public Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetBytesAsync(key, value, ttl, cancellationToken);
        });
    }

    public Task SetCounterAsync(string key, long value, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetCounterAsync(key, value, cancellationToken);
        });
    }

    public Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetMapAsync(key, map, ttl, cancellationToken);
        });
    }

    public Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetMapValueAsync(key, subKey, subValue, ttl, cancellationToken);
        });
    }

    public Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, cancellationToken);
        });
    }

    public Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetObjectJsonAsync(key, t, jsonSerializerOptions, ttl, cancellationToken);
        });
    }

    public Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SubscribeAsync(channel, cancellationToken);
        });
    }

    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        return RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.UnsubscribeAsync(channel, cancellationToken);
        });
    }

    public async ValueTask DisposeAsync()
        => await this.DisconnectAsync();
}
