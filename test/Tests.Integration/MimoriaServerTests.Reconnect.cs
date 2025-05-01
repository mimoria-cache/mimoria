// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(2_000)]
    [InlineData(5_000)]
    [InlineData(10_000)]
    public async Task Reconnect_Given_MimoriaClient_When_ServerRestartsAndAfterwardsSetAndGetString_Then_CorrectValueIsReturned(int serverDowntimeMs)
    {
        // Arrange
        const string key = "string:reconnect:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Assert
        Assert.True(mimoriaClient.IsConnected);

        // Act
        await this.mimoriaServerOne.StopAsync();

        await Task.Delay(serverDowntimeMs);

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

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Reconnect_Given_MimoriaClient_When_ServerRestartsAndReconnectAndRestartAfterwardsSetAndGetString_Then_CorrectValueIsReturned(int restartCount)
    {
        // Arrange
        const string key = "string:reconnect:key";
        const string value = "value";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        // Assert
        Assert.True(mimoriaClient.IsConnected);

        for (int i = 0; i < restartCount; i++)
        {
            // Act
            await this.mimoriaServerOne.StopAsync();

            await Task.Delay(1_000);

            // Assert
            Assert.False(mimoriaClient.IsConnected);

            // Act
            await this.mimoriaServerOne.StartAsync();

            await Task.Delay(5_000);

            // Assert
            Assert.True(mimoriaClient.IsConnected);
        }

        // Act (verify it's working)
        await mimoriaClient.SetStringAsync(key, value);
        string? actualValue = await mimoriaClient.GetStringAsync(key);

        // Assert
        Assert.Equal(value, actualValue);
    }
}
