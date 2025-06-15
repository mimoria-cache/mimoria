// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Text;

using Varelen.Mimoria.Client;
using Varelen.Mimoria.Core;
using Varelen.Mimoria.Core.Buffer;

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

        await this.mimoriaServerOne.StopAsync();

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
    public async Task Operations_Given_MimoriaClient_When_SetObjectBinaryGetObjectBinary_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "binary:key";
        var value = new User(4, "Mimoria");

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetObjectBinaryAsync(key, value);
        User? actualValue = await mimoriaClient.GetObjectBinaryAsync<User>(key);

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
    public async Task Operations_Given_MimoriaClient_When_AddListContainsListGetList_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "list:key";
        const string value = "one";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.AddListAsync(key, value);
        await mimoriaClient.AddListAsync(key, value);

        bool contains = await mimoriaClient.ContainsListAsync(key, value);
        bool containsNot = await mimoriaClient.ContainsListAsync(key, $"{value}random");

        ImmutableList<string> actualValue = await mimoriaClient.GetListAsync(key);

        // Assert
        Assert.True(contains);
        Assert.False(containsNot);
        Assert.Equal([value, value], actualValue);
    }

    [Theory]
    [InlineData("user:one", Comparison.StartsWith, new string[] { "user:one:two", "user:one:three" })]
    [InlineData("user:two", Comparison.StartsWith, new string[] { "user:two:three" })]
    [InlineData("three", Comparison.EndsWith, new string[] { "user:one:three", "user:two:three" })]
    [InlineData(":four", Comparison.EndsWith, new string[] { "user:three:four" })]
    [InlineData("one", Comparison.Contains, new string[] { "user:one:two", "user:one:three" })]
    [InlineData("two", Comparison.Contains, new string[] { "user:one:two", "user:two:three" })]
    public async Task Operations_Given_MimoriaClient_When_DeleteByPattern_Then_CorrectKeysAreDeleted(string pattern, Comparison comparison, string[] expectedKeys)
    {
        // Arrange
        await using var mimoriaClient = await this.ConnectToServerAsync();

        const int size = 4;
        const string value = "value";

        await mimoriaClient.SetStringAsync("user:one:two", value);
        await mimoriaClient.AddListAsync("user:one:three", value);
        await mimoriaClient.SetBytesAsync("user:two:three", [100, 101, 102, 103]);
        await mimoriaClient.SetCounterAsync("user:three:four", 100);

        // Act
        ulong deleted = await mimoriaClient.DeleteAsync(pattern, comparison);

        // Assert
        var actualKeys = new List<string>(capacity: expectedKeys.Length);
        foreach (var key in expectedKeys)
        {
            if (await mimoriaClient.ExistsAsync(key))
            {
                actualKeys.Add(key);
            }
        }

        Assert.Equal((ulong)expectedKeys.Length, size - deleted);
        Assert.Equal(expectedKeys.Length, actualKeys.Count);
        Assert.Equal(expectedKeys, actualKeys);
    }

    [Fact]
    public async Task Operations_Given_MimoriaClient_When_Clear_Then_CacheIsCleared()
    {
        // Arrange
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        await mimoriaClient.AddListAsync("clear:list", value);
        await mimoriaClient.SetStringAsync("clear:string", value);
        await mimoriaClient.SetBytesAsync("clear:bytes", Encoding.UTF8.GetBytes(value));

        Stats statsBefore = await mimoriaClient.GetStatsAsync();

        // Act
        await mimoriaClient.ClearAsync();

        // Assert
        string? stringValue = await mimoriaClient.GetStringAsync("clear:string");
        Stats stats = await mimoriaClient.GetStatsAsync();

        Assert.Equal((ulong)0, stats.CacheSize);
        Assert.Equal((ulong)3, statsBefore.CacheSize);
        Assert.Null(stringValue);
    }

    private class User : IBinarySerializable
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        public User()
        {
            
        }

        public User(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        public void Deserialize(IByteBuffer byteBuffer)
        {
            this.Id = byteBuffer.ReadInt();
            this.Name = byteBuffer.ReadString();
        }

        public void Serialize(IByteBuffer byteBuffer)
        {
            byteBuffer.WriteInt(this.Id);
            byteBuffer.WriteString(this.Name);
        }

        public override bool Equals(object? obj)
            => obj is User user &&
                   this.Id == user.Id &&
                   this.Name == user.Name;

        public override int GetHashCode()
            => HashCode.Combine(this.Id, this.Name);
    }
}
