// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

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
}
