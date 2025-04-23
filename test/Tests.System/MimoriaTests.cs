// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.System;

public class MimoriaTests : IClassFixture<MimoriaContainerFixture>
{
    private readonly MimoriaContainerFixture mimoriaContainerFixture;

    public MimoriaTests(MimoriaContainerFixture mimoriaContainerFixture)
    {
        this.mimoriaContainerFixture = mimoriaContainerFixture;
    }

    [Fact]
    public async Task MimoriaTests_When_SetStringAndGetString_Then_CorrectValueIsReturned()
    {
        // Arrange
        var mimoriaClient = new MimoriaClient(MimoriaContainerFixture.Ip, this.mimoriaContainerFixture.Container.GetMappedPublicPort(6565), MimoriaContainerFixture.Password);
        await mimoriaClient.ConnectAsync();

        // Act
        await mimoriaClient.SetStringAsync("key", "value");
        string? value = await mimoriaClient.GetStringAsync("key");

        // Assert
        Assert.Equal("value", value);
    }

    [Fact]
    public async Task MimoriaTests_When_Bulk_Then_CorrectValuesAreReturned()
    {
        // Arrange
        var mimoriaClient = new MimoriaClient(MimoriaContainerFixture.Ip, this.mimoriaContainerFixture.Container.GetMappedPublicPort(6565), MimoriaContainerFixture.Password);
        await mimoriaClient.ConnectAsync();

        // Act
        var bulk = mimoriaClient.Bulk();
        bulk.AddList("list", "one");
        bulk.AddList("list", "two");
        bulk.ContainsList("list", "three");

        ImmutableList<object?> result = await bulk.ExecuteAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True((bool)result[0]!);
        Assert.True((bool)result[1]!);
        Assert.False((bool)result[2]!);
    }

    [Fact]
    public async Task MimoriaTests_When_IncrementCounter_Then_CorrectValueIsReturned()
    {
        // Arrange
        var mimoriaClient = new MimoriaClient(MimoriaContainerFixture.Ip, this.mimoriaContainerFixture.Container.GetMappedPublicPort(6565), MimoriaContainerFixture.Password);
        await mimoriaClient.ConnectAsync();

        // Act
        await mimoriaClient.IncrementCounterAsync("counter", increment: 668);
        await mimoriaClient.IncrementCounterAsync("counter", increment: 668);
        await mimoriaClient.IncrementCounterAsync("counter", increment: 1);

        // Assert
        long value = await mimoriaClient.GetCounterAsync("counter");
        Assert.Equal(1337, value);
    }
}
