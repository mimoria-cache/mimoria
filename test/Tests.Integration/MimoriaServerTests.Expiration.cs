// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    [Fact]
    public async Task Expiration_Given_MimoriaClient_When_SetStringWithExpireAndWaitGetString_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "string:expiration:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetStringAsync(key, value, TimeSpan.FromMilliseconds(10));
        await Task.Delay(50);
        string? actualValue = await mimoriaClient.GetStringAsync(key);

        // Assert
        Assert.Null(actualValue);
    }

    [Fact]
    public async Task Expiration_Given_MimoriaClient_When_SetStringWithExpireAndGetStringAndWaitGetString_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "string:expiration:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Act
        await mimoriaClient.SetStringAsync(key, value, TimeSpan.FromMilliseconds(500));
        string? actualValueFirst = await mimoriaClient.GetStringAsync(key);

        await Task.Delay(1_000);

        string? actualValueSecond = await mimoriaClient.GetStringAsync(key);

        // Assert
        Assert.Equal(value, actualValueFirst);
        Assert.Null(actualValueSecond);
    }
}
