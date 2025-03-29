// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    [Fact]
    public async Task Replication_Given_TwoNodes_When_WriteToPrimary_WithSyncReplication_Then_SecondaryAlsoHasKey()
    {
        // Arrange
        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act
        await clusterMimoriaClient.SetStringAsync("key", "value");

        string? value = await clusterMimoriaClient.GetStringAsync("key", preferSecondary: true);

        // Assert
        Assert.Equal("value", value);
    }
}
