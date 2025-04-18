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

/// <summary>
/// A client implementation that connects to a cluster of Mimoria servers.
/// </summary>
public sealed class ClusterMimoriaClient : IClusterMimoriaClient
{
    private const int DefaultRetryCount = 6;
    private const int DefaultRetryDelay = 1_000;

    private readonly List<IMimoriaClient> mimoriaClients;
    private readonly Dictionary<string, List<Subscription>> subscriptions;
    private readonly string password;
    private readonly IPEndPoint[] ipEndPoints;
    private readonly int retryCount;
    private readonly int retryDelay;

    /// <inheritdoc />
    public int? ServerId => this.mimoriaClients.Where(mimoriaClient => mimoriaClient.IsPrimary).First().ServerId;

    /// <inheritdoc />
    public bool IsConnected => this.mimoriaClients.All(mimoriaClient => mimoriaClient.IsConnected);

    /// <inheritdoc />
    public bool IsPrimary { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public IReadOnlyList<IMimoriaClient> MimoriaClients => this.mimoriaClients;

    /// <summary>
    /// Creates a new cluster client with the default retry count and delay.
    /// </summary>
    /// <param name="password">The password of the servers.</param>
    /// <param name="ipEndPoints">The server IP endpoints.</param>
    public ClusterMimoriaClient(string password, params IPEndPoint[] ipEndPoints)
        : this(password, DefaultRetryCount, DefaultRetryDelay, ipEndPoints)
    {

    }

    /// <summary>
    /// Creates a new cluster client with the specified retry count and delay.
    /// </summary>
    /// <param name="password">The password of the servers.</param>
    /// <param name="retryCount">The retry count.</param>
    /// <param name="retryDelay">The retry delay.</param>
    /// <param name="ipEndPoints">The server IP endpoints.</param>
    public ClusterMimoriaClient(string password, int retryCount, int retryDelay, params IPEndPoint[] ipEndPoints)
    {
        this.password = password;
        this.retryCount = retryCount;
        this.retryDelay = retryDelay;
        this.ipEndPoints = ipEndPoints;
        this.mimoriaClients = new List<IMimoriaClient>();
        this.subscriptions = new Dictionary<string, List<Subscription>>();
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

        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            if (preferSecondary && mimoriaClient.IsPrimary)
            {
                continue;
            }

            if (mimoriaClient.IsConnected)
            {
                return mimoriaClient;
            }

            readingMimoriaClient ??= mimoriaClient;
        }

        return readingMimoriaClient ?? throw new NoSecondaryAvailableException();
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IPEndPoint remoteEndPoint in this.ipEndPoints)
        {
            var mimoriaClient = new MimoriaClient(remoteEndPoint, this.password);

            await mimoriaClient.ConnectAsync(cancellationToken);

            var subscription = await mimoriaClient.SubscribeAsync(Channels.PrimaryChanged, cancellationToken);
            subscription.Payload += HandlePrimaryChangedAsync;

            this.mimoriaClients.Add(mimoriaClient);
        }
    }

    private async ValueTask HandlePrimaryChangedAsync(MimoriaValue payload)
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

            foreach (var (channel, subs) in this.subscriptions)
            {
                await newPrimary.SubscribeInternalAsync(channel, subs);
            }
        }
    }

    private async Task RetryPrimaryOperationAsync(Func<Task> operationAsync, int retry = 1)
    {
        if (retry > this.retryCount)
        {
            throw new TimeoutException("No primary was available even after extended retrying");
        }

        try
        {
            await operationAsync();
        }
        catch (Exception exception) when (exception is TimeoutException or NoPrimaryAvailableException)
        {
            await Task.Delay(this.retryDelay);

            await this.RetryPrimaryOperationAsync(operationAsync, retry + 1);
        }
    }

    private async Task<T> RetryPrimaryOperationAsync<T>(Func<Task<T>> operationAsync, int retry = 1)
    {
        if (retry > this.retryCount)
        {
            throw new TimeoutException("No primary was available even after extended retrying");
        }

        try
        {
            return await operationAsync();
        }
        catch (Exception exception) when (exception is TimeoutException or NoPrimaryAvailableException)
        {
            await Task.Delay(this.retryDelay);

            return await this.RetryPrimaryOperationAsync(operationAsync, retry + 1);
        }
    }

    private async Task<T> RetrySecondaryOperationAsync<T>(Func<Task<T>> operationAsync, int retry = 1)
    {
        if (retry > this.retryCount)
        {
            throw new TimeoutException("No server for reading was available even after extended retrying");
        }

        try
        {
            return await operationAsync();
        }
        catch (Exception exception) when (exception is TimeoutException or NoSecondaryAvailableException)
        {
            await Task.Delay(this.retryDelay);

            return await this.RetrySecondaryOperationAsync(operationAsync, retry + 1);
        }
    }

    /// <inheritdoc />
    public Task SetStringAsync(string key, string? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetStringAsync(key, value, ttl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<string?> GetStringAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetStringAsync(key, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
        => this.GetStringAsync(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task AddListAsync(string key, string value, TimeSpan ttl = default, TimeSpan valueTtl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.AddListAsync(key, value, ttl, valueTtl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public IBulkOperation Bulk()
    {
        IMimoriaClient mimoriaClient = this.GetPrimary();
        return new BulkOperation((MimoriaClient)mimoriaClient);
    }

    /// <inheritdoc />
    public Task<bool> ContainsListAsync(string key, string value, CancellationToken cancellationToken = default)
        => this.ContainsListAsync(key, value, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ContainsListAsync(string key, string value, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.ContainsListAsync(key, value, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<long> DecrementCounterAsync(string key, long decrement, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.DecrementCounterAsync(key, decrement, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.DeleteAsync(key, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (IMimoriaClient mimoriaClient in this.mimoriaClients)
        {
            await mimoriaClient.DisconnectAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => this.ExistsAsync(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.ExistsAsync(key, cancellationToken).AsTask();
        }));
    }

    /// <inheritdoc />
    public Task<byte[]?> GetBytesAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetBytesAsync(key, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
        => this.GetBytesAsync(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default)
        => this.GetListAsync(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task<List<string>> GetListAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetListAsync(key, cancellationToken);
        });
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> GetListEnumerableAsync(string key, CancellationToken cancellationToken = default)
        => this.GetListEnumerableAsync(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<string> GetListEnumerableAsync(string key, bool preferSecondary, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<string> listEnumerable = await this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return Task.FromResult(mimoriaClient.GetListEnumerableAsync(key, cancellationToken));
        });
        
        await foreach (string value in listEnumerable)
        {
            yield return value;
        }
    }

    /// <inheritdoc />
    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, CancellationToken cancellationToken = default)
        => this.GetMapAsync(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task<Dictionary<string, MimoriaValue>> GetMapAsync(string key, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetMapAsync(key, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, bool preferSecondary, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetMapValueAsync(key, subKey, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<MimoriaValue> GetMapValueAsync(string key, string subKey, CancellationToken cancellationToken = default)
        => this.GetMapValueAsync(key, subKey, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task<T?> GetObjectBinaryAsync<T>(string key, bool preferSecondary, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetObjectBinaryAsync<T>(key, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<T?> GetObjectBinaryAsync<T>(string key, CancellationToken cancellationToken = default) where T : IBinarySerializable, new()
        => this.GetObjectBinaryAsync<T>(key, preferSecondary: false, cancellationToken);

    /// <inheritdoc />
    public Task<T?> GetObjectJsonAsync<T>(string key, bool preferSecondary, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
    {
        return this.RetrySecondaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetReadingClient(preferSecondary);
            return mimoriaClient.GetObjectJsonAsync<T>(key, jsonSerializerOptions, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<T?> GetObjectJsonAsync<T>(string key, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        => this.GetObjectJsonAsync<T>(key, preferSecondary: false, jsonSerializerOptions, cancellationToken);

    /// <inheritdoc />
    public Task<Stats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.GetStatsAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<long> IncrementCounterAsync(string key, long increment, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return await RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.IncrementCounterAsync(key, increment, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<long> GetCounterAsync(string key, CancellationToken cancellationToken = default)
        => this.IncrementCounterAsync(key, increment: 0, fireAndForget: false, cancellationToken);

    /// <inheritdoc />
    public Task RemoveListAsync(string key, string value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.RemoveListAsync(key, value, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task SetBytesAsync(string key, byte[]? value, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetBytesAsync(key, value, ttl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task SetCounterAsync(string key, long value, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetCounterAsync(key, value, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task SetMapAsync(string key, Dictionary<string, MimoriaValue> map, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetMapAsync(key, map, ttl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task SetMapValueAsync(string key, string subKey, MimoriaValue subValue, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetMapValueAsync(key, subKey, subValue, ttl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task SetObjectBinaryAsync(string key, IBinarySerializable? binarySerializable, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetObjectBinaryAsync(key, binarySerializable, ttl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task SetObjectJsonAsync<T>(string key, T? t, JsonSerializerOptions? jsonSerializerOptions = null, TimeSpan ttl = default, bool fireAndForget = false, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SetObjectJsonAsync(key, t, jsonSerializerOptions, ttl, fireAndForget, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task PublishAsync(string channel, MimoriaValue payload, CancellationToken cancellationToken = default)
    {
        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.PublishAsync(channel, payload, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<Subscription> SubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        Subscription subscription = await RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.SubscribeAsync(channel, cancellationToken);
        });

        if (!this.subscriptions.TryGetValue(channel, out List<Subscription>? subscriptions))
        {
            subscriptions = new List<Subscription>
            {
                subscription
            };
            this.subscriptions.Add(channel, subscriptions);
        }
        else
        {
            subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <inheritdoc />
    public Task UnsubscribeAsync(string channel, CancellationToken cancellationToken = default)
    {
        _ = this.subscriptions.Remove(channel);

        return this.RetryPrimaryOperationAsync(() =>
        {
            IMimoriaClient mimoriaClient = this.GetPrimary();
            return mimoriaClient.UnsubscribeAsync(channel, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await this.DisconnectAsync();
}
