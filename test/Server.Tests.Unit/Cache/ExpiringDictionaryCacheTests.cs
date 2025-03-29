// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.Metrics;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Server.Tests.Unit.Cache;

public class ExpiringDictionaryCacheTests
{
    private const int MaxTestListCount = 10;
    private const int MaxTestMapCount = 10;

    private readonly IMimoriaMetrics metrics;

    public ExpiringDictionaryCacheTests()
    {
        this.metrics = Substitute.For<IMimoriaMetrics>();
    }

    [Fact]
    public async Task GetString_When_SetString_And_GetString_Then_CorrectValueIsReturnedAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        // Act
        await sut.SetStringAsync("key", "Mimoria", 0);

        string? value = await sut.GetStringAsync("key");

        // Assert
        Assert.Equal("Mimoria", value);
    }

    [Fact]
    public async Task GetString_When_SetString_And_GetString_AfterExpireTime_Then_NullIsReturnedAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromMilliseconds(500));

        // Act
        await sut.SetStringAsync("key", "Mimoria", 100);

        string? firstValue = await sut.GetStringAsync("key");

        await Task.Delay(500);

        string? secondValue = await sut.GetStringAsync("key");

        // Assert
        Assert.Equal("Mimoria", firstValue);
        Assert.Null(secondValue);
    }

    [Fact]
    public async Task GetBytes_When_SetBytes_And_GetBytes_Then_CorrectValueIsReturnedAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";
        byte[] value = [1, 2, 3, 4];

        // Act
        await sut.SetBytesAsync(key, value, 0);

        byte[]? actualValue = await sut.GetBytesAsync(key);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task GetMap_When_SetMap_And_GetMap_Then_CorrectValueIsReturnedAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";
        var value = new Dictionary<string, MimoriaValue>
        {
            { "one", 2.4f },
            { "two", 2.4d },
            { "three", "value" },
            { "four", true },
            { "five", new byte[] { 1, 2, 3, 4 } }
        };

        // Act
        await sut.SetMapAsync(key, value, 0);

        Dictionary<string, MimoriaValue>? actualValue = await sut.GetMapAsync(key);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task AddList_When_AddList_And_ReachingMaxCount_Then_ArgumentExceptionIsThrown()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";

        // Act
        for (int i = 0; i < MaxTestListCount; i++)
        {
            await sut.AddListAsync(key, $"value{i}", 0, 0, MaxTestListCount);
        }

        // Act & Assert
        var argumentException = await Assert.ThrowsAsync<ArgumentException>(() => sut.AddListAsync(key, "value", 0, 0, MaxTestListCount));
        Assert.Equal($"List under key '{key}' has reached its maximum count of '{MaxTestListCount}'", argumentException.Message);
    }

    [Fact]
    public async Task AddList_When_AddListWithDuplicates_Then_BothValuesAreAdded()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";

        // Act
        await sut.AddListAsync(key, "value", 0, 0, MaxTestListCount);
        await sut.AddListAsync(key, "value", 0, 0, MaxTestListCount);

        // Assert
        var values = new List<string>();
        await foreach (string value in sut.GetListAsync(key))
        {
            values.Add(value);
        }
        
        Assert.Equal(1U, sut.Size);
        Assert.Equal(2, values.Count);
        Assert.Equal("value", values[0]);
        Assert.Equal("value", values[1]);
    }

    [Fact]
    public async Task AddList_When_AddListWithDuplicates_And_Remove_Then_OnlyFirstValueIsRemoved()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";

        await sut.AddListAsync(key, "value", 0, 0, MaxTestListCount);
        await sut.AddListAsync(key, "value", 0, 0, MaxTestListCount);

        // Act
        await sut.RemoveListAsync(key, "value");

        // Assert
        var values = new List<string>();
        await foreach (string value in sut.GetListAsync(key))
        {
            values.Add(value);
        }

        Assert.Equal(1U, sut.Size);
        Assert.Single(values);
        Assert.Equal("value", values[0]);
    }

    [Fact]
    public async Task AddList_When_AddListWithValueExpire_Then_ExpiredAreRemoved()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromMilliseconds(500));

        const string key = "key";

        // Act
        await sut.AddListAsync(key, "value1", 0, valueTtlMilliseconds: 250, MaxTestListCount);
        await sut.AddListAsync(key, "value2", 0, valueTtlMilliseconds: 250, MaxTestListCount);
        await sut.AddListAsync(key, "value3", 0, valueTtlMilliseconds: 5_000, MaxTestListCount);

        await Task.Delay(1_000);

        // Assert
        var values = new List<string>();
        await foreach (string value in sut.GetListAsync(key))
        {
            values.Add(value);
        }

        Assert.Equal(1U, sut.Size);
        Assert.Single(values);
        Assert.Equal("value3", values[0]);
    }

    [Fact]
    public async Task SetMapValue_When_SetMapValue_And_ReachingMaxCount_Then_ArgumentExceptionIsThrown()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";
        const string subKey = "subkey";

        // Act
        for (int i = 0; i < MaxTestMapCount; i++)
        {
            await sut.SetMapValueAsync(key, $"{subKey}{i}", "value", 0, MaxTestMapCount);
        }

        // Act & Assert
        var argumentException = await Assert.ThrowsAsync<ArgumentException>(() => sut.SetMapValueAsync(key, $"{subKey}{MaxTestMapCount}", "value", 0, MaxTestMapCount));
        Assert.Equal($"Map under key '{key}' has reached its maximum count of '{MaxTestMapCount}'", argumentException.Message);

        var map = await sut.GetMapAsync(key);
        Assert.Equal((ulong)MaxTestMapCount, (ulong)map.Count);
    }

    [Fact]
    public async Task SetMapValue_When_SetMapValue_And_SettingSameKey_Then_NoExceptionIsThrown()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";
        const string subKey = "subkey";

        // Act
        for (int i = 0; i < MaxTestMapCount; i++)
        {
            await sut.SetMapValueAsync(key, $"{subKey}{i}", "value", 0, MaxTestMapCount);
        }

        await sut.SetMapValueAsync(key, $"{subKey}{MaxTestMapCount - 1}", "value", 0, MaxTestMapCount);

        // Assert
        var map = await sut.GetMapAsync(key);
        Assert.Equal((ulong)MaxTestMapCount, (ulong)map.Count);
    }

    [Fact]
    public async Task ConcurrentCleanupAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromMilliseconds(1));

        const int IterationCount = 1_000_000;

        // Act
        for (int i = 0; i < IterationCount; i++)
        {
            await sut.SetStringAsync("key" + i, "value" + i, ttlMilliseconds: 50);
            await sut.GetStringAsync("key" + i);
        }

        await Task.Delay(500);

        // Assert
        Assert.Equal((ulong)0, sut.Size);
        Assert.True(sut.Hits > 0);
        Assert.True(sut.ExpiredKeys > 0);
    }

    [Fact]
    public async Task Concurrent_SetDeleteAndGetStringAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromMilliseconds(1));

        const int TaskCount = 10;
        const int IterationCount = 10_000;
        const ulong TotalIterationCount = TaskCount * IterationCount;

        int run = 0;
        ulong operations = 0;

        // Act
        var tasks = new List<Task>(capacity: TaskCount);

        for (int i = 0; i < TaskCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await sut.SetStringAsync("key", "value", 0);
                    await sut.DeleteAsync("key");
                    await sut.GetStringAsync("key");

                    Interlocked.Increment(ref operations);
                }

                Interlocked.Increment(ref run);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(TaskCount, run);
        Assert.Equal(TotalIterationCount, operations);
        Assert.Equal((ulong)0, sut.Size);
        Assert.Equal(TotalIterationCount, sut.Hits + sut.Misses);
    }

    [Fact]
    public async Task Concurrent_CounterIncrementAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.FromMilliseconds(1));

        const string Key = "key";
        const int TaskCount = 10;
        const int IterationCount = 10_000;
        const ulong TotalIterationCount = TaskCount * IterationCount;

        int run = 0;
        ulong operations = 0;

        // Act
        var tasks = new List<Task>(capacity: TaskCount);

        for (int i = 0; i < TaskCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await sut.IncrementCounterAsync(Key, 1);

                    Interlocked.Increment(ref operations);
                }

                Interlocked.Increment(ref run);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(TaskCount, run);
        Assert.Equal(TotalIterationCount, operations);
        Assert.Equal((ulong)1, sut.Size);
        Assert.Equal(TotalIterationCount, sut.Hits + sut.Misses);
        long counterValue = await sut.IncrementCounterAsync(Key, 0);
        Assert.Equal(TotalIterationCount, (ulong)counterValue);
    }

    [Fact]
    public async Task Concurrent_AddRemoveGetListAsync()
    {
        // Arrange
        using var sut = this.CreateCache(TimeSpan.Zero);

        const int TaskCount = 10;
        const int IterationCount = 10_000;
        const int TotalIterationCount = TaskCount * IterationCount;

        int run = 0;
        int operations = 0;

        // Act
        var tasks = new List<Task>(capacity: TaskCount);

        for (int i = 0; i < TaskCount; i++)
        {
            int ii = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < IterationCount; i++)
                {
                    await sut.AddListAsync("key", "value", ttlMilliseconds: 0, valueTtlMilliseconds: 0, ProtocolDefaults.MaxListCount);
                    await sut.RemoveListAsync("key", "value");
                    await foreach (var item in sut.GetListAsync("key"))
                    {

                    }

                    Interlocked.Increment(ref operations);
                }

                Interlocked.Increment(ref run);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(TaskCount, run);
        Assert.Equal(TotalIterationCount, operations);
        Assert.Equal((ulong)0, sut.Size);
    }

    private ExpiringDictionaryCache CreateCache(TimeSpan expireCheckInterval)
        => new(NullLogger<ExpiringDictionaryCache>.Instance, this.metrics, new PubSubService(NullLogger<PubSubService>.Instance, this.metrics), expireCheckInterval);
}
