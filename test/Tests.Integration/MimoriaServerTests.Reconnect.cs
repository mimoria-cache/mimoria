// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    [Fact]
    public async Task Reconnect_Given_MimoriaClient_When_ServerRestartsAndAfterwardsSetAndGetString_Then_CorrectValueIsReturned()
    {
        // Arrange
        const string key = "string:reconnect:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Assert
        Assert.True(mimoriaClient.IsConnected);

        // Act
        this.mimoriaServerOne.Stop();

        await Task.Delay(1_000);

        // Assert
        Assert.False(mimoriaClient.IsConnected);

        // Act
        await this.mimoriaServerOne.StartAsync();

        await Task.Delay(5_000);

        // Assert
        Assert.True(mimoriaClient.IsConnected);

        // Act (verify it's working)
        await mimoriaClient.SetStringAsync(key, value);
        string? actualValue = await mimoriaClient.GetStringAsync(key);

        // Assert
        Assert.Equal(value, actualValue);
    }
}
