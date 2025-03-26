// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerTests : IAsyncLifetime
{
    [Fact]
    public async Task PubSub_Given_MimoriaClient_When_SubscribeAndReconnectAndPublish_Then_PayloadEventIsCalled()
    {
        // Arrange
        const string channel = "pubsub:test";
        const string payload = "test";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        string? receivedPayload = null;

        Subscription subscription = await mimoriaClient.SubscribeAsync(channel);
        subscription.Payload += (payload) =>
        {
            receivedPayload = payload;
        };

        // Act 1
        this.mimoriaServerOne.Stop();

        await Task.Delay(1_000);

        await this.mimoriaServerOne.StartAsync();

        // Assert 1
        await Task.Delay(5_000);

        Assert.True(mimoriaClient.IsConnected);

        // Act 2
        await mimoriaClient.PublishAsync(channel, payload);

        await Task.Delay(500);

        // Assert 2
        Assert.Equal(payload, receivedPayload);
    }
}
