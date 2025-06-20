﻿// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client;

namespace Varelen.Mimoria.Tests.Integration;

public partial class MimoriaServerClusterTests : IAsyncLifetime
{
    [Fact]
    public async Task PubSub_Given_TwoNodes_When_SubscribeAndPublishString_Then_PayloadEventIsCalled()
    {
        // Arrange
        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        string? value = null;

        // Act
        Subscription subscription = await clusterMimoriaClient.SubscribeAsync("test");
        subscription.Payload += payload =>
        {
            value = payload;
            return ValueTask.CompletedTask;
        };
        await clusterMimoriaClient.PublishAsync("test", "value");

        await Task.Delay(500);

        // Assert
        Assert.Equal("value", value);
    }

    [Fact]
    public async Task PubSub_Given_TwoNodes_When_SubscribeAndPublishBytes_Then_PayloadEventIsCalled()
    {
        // Arrange
        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        byte[]? value = null;

        // Act
        Subscription subscription = await clusterMimoriaClient.SubscribeAsync("test");
        subscription.Payload += payload =>
        {
            value = payload;
            return ValueTask.CompletedTask;
        };
        await clusterMimoriaClient.PublishAsync("test", new byte[] { 1, 2, 3, 4 });

        await Task.Delay(500);

        // Assert
        Assert.Equal([1, 2, 3, 4], value);
    }

    [Fact]
    public async Task PubSub_Given_TwoNodes_When_SubscribeAndUnsubscribeAndPublish_Then_PayloadEventIsNotCalled()
    {
        // Arrange
        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        string? value = null;

        // Act
        Subscription subscription = await clusterMimoriaClient.SubscribeAsync("test");
        subscription.Payload += payload =>
        {
            value = payload;
            return ValueTask.CompletedTask;
        };

        await clusterMimoriaClient.UnsubscribeAsync("test");

        await clusterMimoriaClient.PublishAsync("test", "value");

        await Task.Delay(500);

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task PubSub_Given_TwoNodes_When_PrimaryGoesDown_Then_NewLeaderIsPublishedInPrimaryChangedChannel()
    {
        // Arrange
        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        // Act & Assert
        Assert.Equal(2, clusterMimoriaClient.ServerId);

        await this.mimoriaServerTwo.StopAsync();

        await Task.Delay(6_000);

        Assert.Equal(1, clusterMimoriaClient.ServerId);
    }

    [Fact]
    public async Task PubSub_Given_TwoNodes_When_PrimaryGoesDownAndPublish_Then_PayloadEventIsStillCalled()
    {
        // Arrange
        await using var clusterMimoriaClient = await this.ConnectToClusterAsync();

        string? value = null;

        // Act
        Subscription subscription = await clusterMimoriaClient.SubscribeAsync("test");
        subscription.Payload += payload =>
        {
            value = payload;
            return ValueTask.CompletedTask;
        };

        await this.mimoriaServerTwo.StopAsync();

        await Task.Delay(6_000);

        await clusterMimoriaClient.PublishAsync("test", "value");

        await Task.Delay(500);

        // Assert
        Assert.Equal("value", value);
    }
}
