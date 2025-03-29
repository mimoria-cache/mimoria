// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client;
using Varelen.Mimoria.Core;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    [Fact]
    public async Task Operations_Given_MimoriaClient_When_SetStringGetString_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "string:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetStringAsync(key, value);
        string? actualValue = await mimoriaClient.GetStringAsync(key);

        // Assert
        Assert.Equal("value", actualValue);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_ServerStopped_SetStringGetString_Then_TimeoutExceptionIsThrown()
    {
        // Arrange
        const string key = "string:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        this.mimoriaServerOne.Stop();

        // Act & Assert
        var timeoutException = await Assert.ThrowsAsync<TimeoutException>(() => mimoriaClient.SetStringAsync(key, value));
        Assert.Equal("The operation has timed out.", timeoutException.Message);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_SetBytesGetBytes_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "bytes:key";
        byte[] value = [1, 2, 3, 4, 5];

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetBytesAsync(key, value);
        byte[]? actualValue = await mimoriaClient.GetBytesAsync(key);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_SetObjectJsonGetObjectJson_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "json:key";
        var value = new User(2, "Mimoria");

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetObjectJsonAsync(key, value);
        User? actualValue = await mimoriaClient.GetObjectJsonAsync<User>(key);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_IncrementCounter_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "counter:key";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        long one = await mimoriaClient.IncrementCounterAsync(key, increment: 100);
        long two = await mimoriaClient.IncrementCounterAsync(key, increment: 100);

        // Assert
        Assert.Equal(100, one);
        Assert.Equal(200, two);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_DecrementCounter_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "counter:key";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        long one = await mimoriaClient.DecrementCounterAsync(key, decrement: 100);
        long two = await mimoriaClient.DecrementCounterAsync(key, decrement: 100);

        // Assert
        Assert.Equal(-100, one);
        Assert.Equal(-200, two);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_SetCounterAndIncrementByZero_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "counter:key";
        const int value = 1337;

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetCounterAsync(key, value);
        long actualValue = await mimoriaClient.IncrementCounterAsync(key, increment: 0);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_GetStats_Then_CorrectValueIsReturned()
    {
        // Arrange
        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        Stats stats = await mimoriaClient.GetStatsAsync();

        // Assert
        Assert.True(stats.Connections > 0);
        Assert.Equal(0, stats.CacheHitRatio);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_SetMapGetMap_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "map:key";
        var value = new Dictionary<string, MimoriaValue>
        {
            { "one", 2.4f },
            { "two", 2.4d },
            { "three", "value" },
            { "four", true },
            { "five", new byte[] { 1, 2, 3, 4 } }
        };

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetMapAsync(key, value);
        Dictionary<string, MimoriaValue> actualValue = await mimoriaClient.GetMapAsync(key);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_SetMapValueGetMapValue_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "map:key";
        const string subKey = "subkey";
        var value = new byte[] { 1, 2, 3, 4 };

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetMapValueAsync(key, subKey, value);
        MimoriaValue actualValue = await mimoriaClient.GetMapValueAsync(key, subKey);

        // Assert
        Assert.Equal(value, actualValue.Value);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_AddListGetList_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "list:key";
        const string value = "one";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.AddListAsync(key, value);
        await mimoriaClient.AddListAsync(key, value);
        List<string> actualValue = await mimoriaClient.GetListAsync(key);

        // Assert
        Assert.Equal([value, value], actualValue);
    }

    private record User(int Id, string Name);
}
