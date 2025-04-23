// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    [Fact]
    public async Task Bulk_Given_MimoriaClient_When_BulkSetStringGetString_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "bulk:string:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        IBulkOperation bulkOperation = mimoriaClient.Bulk();
        bulkOperation.SetString(key, value);
        bulkOperation.GetString(key);

        ImmutableList<object?> result = await bulkOperation.ExecuteAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True((bool)result[0]!);
        Assert.Equal(value, result[1]);
    }

    [Fact]
    public async Task Bulk_Given_MimoriaClient_When_BulkIncrementCounter_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "bulk:counter:key";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        IBulkOperation bulkOperation = mimoriaClient.Bulk();
        bulkOperation.IncrementCounter(key, 100);
        bulkOperation.IncrementCounter(key, 200);

        ImmutableList<object?> result = await bulkOperation.ExecuteAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(100, (long)result[0]!);
        Assert.Equal(300, (long)result[1]!);
    }
}
