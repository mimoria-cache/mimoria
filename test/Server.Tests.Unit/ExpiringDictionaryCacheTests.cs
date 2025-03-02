// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Varelen.Mimoria.Core;
using Varelen.Mimoria.Server.Cache;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Server.Tests.Unit;

public class ExpiringDictionaryCacheTests
{
    [Fact]
    public async Task GetString_When_SetString_And_GetString_Then_CorrectValueIsReturnedAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromSeconds(10));

        // Act
        await sut.SetStringAsync("key", "Mimoria", 0);

        string? value = await sut.GetStringAsync("key");

        // Assert
        value.Should().Be("Mimoria");
    }

    [Fact]
    public async Task GetString_When_SetString_And_GetString_AfterExpireTime_Then_NullIsReturnedAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromMilliseconds(500));

        // Act
        await sut.SetStringAsync("key", "Mimoria", 100);

        string? firstValue = await sut.GetStringAsync("key");

        await Task.Delay(500);

        string? secondValue = await sut.GetStringAsync("key");

        // Assert
        firstValue.Should().Be("Mimoria");
        secondValue.Should().BeNull();
    }

    [Fact]
    public async Task GetBytes_When_SetBytes_And_GetBytes_Then_CorrectValueIsReturnedAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromSeconds(10));

        const string key = "key";
        byte[] value = [1, 2, 3, 4];

        // Act
        await sut.SetBytesAsync(key, value, 0);

        byte[]? actualValue = await sut.GetBytesAsync(key);

        // Assert
        actualValue.Should().BeEquivalentTo(value);
    }

    [Fact]
    public async Task GetMap_When_SetMap_And_GetMap_Then_CorrectValueIsReturnedAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromSeconds(10));

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
        actualValue.Should().BeEquivalentTo(value);
    }

    [Fact]
    public async Task ConcurrentCleanupAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromMilliseconds(1));

        const int IterationCount = 1_000_000;

        // Act
        for (int i = 0; i < IterationCount; i++)
        {
            await sut.SetStringAsync("key" + i, "value" + i, ttlMilliseconds: 50);
            await sut.GetStringAsync("key" + i);
        }

        await Task.Delay(500);

        // Assert
        sut.Hits.Should().Be(IterationCount);
        sut.ExpiredKeys.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Concurrent_SetDeleteAndGetStringAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromMilliseconds(1));

        const int TaskCount = 10;
        const int IterationCount = 10_000;
        const int TotalIterationCount = TaskCount * IterationCount;

        int run = 0;
        int operations = 0;

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
        run.Should().Be(TaskCount);
        operations.Should().Be(TotalIterationCount);
        sut.Size.Should().Be(0);
        (sut.Hits + sut.Misses).Should().Be(TotalIterationCount);
    }

    [Fact]
    public async Task Concurrent_CounterIncrementAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.FromMilliseconds(1));

        const string Key = "key";
        const int TaskCount = 10;
        const int IterationCount = 10_000;
        const int TotalIterationCount = TaskCount * IterationCount;

        int run = 0;
        int operations = 0;

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
        run.Should().Be(TaskCount);
        operations.Should().Be(TotalIterationCount);
        sut.Size.Should().Be(1);
        (sut.Hits + sut.Misses).Should().Be(TotalIterationCount);
        long counterValue = await sut.IncrementCounterAsync(Key, 0);
        counterValue.Should().Be(TotalIterationCount);
    }

    [Fact]
    public async Task Concurrent_AddRemoveGetListAsync()
    {
        // Arrange
        using var sut = CreateCache(TimeSpan.Zero);

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
                    await sut.AddListAsync("key", "value", 0);
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
        run.Should().Be(TaskCount);
        operations.Should().Be(TotalIterationCount);
        sut.Size.Should().Be(0);
    }

    private static ExpiringDictionaryCache CreateCache(TimeSpan expireCheckInterval)
        => new(NullLogger<ExpiringDictionaryCache>.Instance, new PubSubService(NullLogger<PubSubService>.Instance), expireCheckInterval);
}
