// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    [Fact]
    public async Task Replication_Given_TwoNodes_When_SetString_WithSyncReplication_Then_SecondaryAlsoHasKey()
    {
        // Arrange
        const string key = "key:replication:string";
        const string value = "value";

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act
        await clusterMimoriaClient.SetStringAsync(key, value);

        string? actualValue = await clusterMimoriaClient.GetStringAsync(key, preferSecondary: true);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task Replication_Given_TwoNodes_When_Delete_WithSyncReplication_Then_SecondaryDoesNotHaveKey()
    {
        // Arrange
        const string key = "key:replication:delete";
        const string value = "value";

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act
        await clusterMimoriaClient.SetStringAsync(key, value);
        await clusterMimoriaClient.DeleteAsync(key);

        string? actualValue = await clusterMimoriaClient.GetStringAsync(key, preferSecondary: true);

        // Assert
        Assert.Null(actualValue);
    }

    [Fact]
    public async Task Replication_Given_TwoNodes_When_AddAndRemoveList_WithSyncReplication_Then_SecondaryHasCorrectValues()
    {
        // Arrange
        const string key = "key:replication:delete";
        string[] values = ["value1", "value2", "value3", "value4"];

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act
        await clusterMimoriaClient.AddListAsync(key, values[0]);
        await clusterMimoriaClient.AddListAsync(key, values[1]);
        await clusterMimoriaClient.AddListAsync(key, values[2]);
        await clusterMimoriaClient.AddListAsync(key, values[3]);

        await clusterMimoriaClient.RemoveListAsync(key, values[2]);

        List<string> actualValues = await clusterMimoriaClient.GetListAsync(key, preferSecondary: true);

        // Assert
        Assert.Equal(3, actualValues.Count);
        Assert.Equal(["value1", "value2", "value4"], actualValues);
    }

    [Fact]
    public async Task Replication_Given_TwoNodes_When_SetBytes_WithSyncReplication_Then_SecondaryAlsoHasKey()
    {
        // Arrange
        const string key = "key:replication:bytes";
        byte[] value = [1, 3, 3, 7];

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act
        await clusterMimoriaClient.SetBytesAsync(key, value);

        byte[]? actualValue = await clusterMimoriaClient.GetBytesAsync(key, preferSecondary: true);

        // Assert
        Assert.Equal(value, actualValue);
    }

    [Fact]
    public async Task Replication_Given_TwoNodes_When_IncrementCounterAndSetCounter_WithSyncReplication_Then_SecondaryAlsoHasKey()
    {
        // Arrange
        const string keyIncrement = "key:replication:counter:increment";
        const string keySet = "key:replication:counter:set";

        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act
        long firstIncrement = await clusterMimoriaClient.IncrementCounterAsync(keyIncrement, 5);
        long secondIncrement = await clusterMimoriaClient.IncrementCounterAsync(keyIncrement, 50);

        await clusterMimoriaClient.SetCounterAsync(keySet, 100);

        // Assert
        long? actualIncrementValue = await this.cacheOne.IncrementCounterAsync(keyIncrement, increment: 0);
        long? actualSetValue = await this.cacheOne.IncrementCounterAsync(keySet, increment: 0);

        Assert.Equal(5, firstIncrement);
        Assert.Equal(55, secondIncrement);
        Assert.Equal(55, actualIncrementValue);
        Assert.Equal(100, actualSetValue);
    }
}
