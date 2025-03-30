// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Client;
using Varelen.Mimoria.Core;

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

    [Fact]
    public async Task PubSub_Given_MimoriaClient_When_SetStringAndDeleteAndDelete_Then_KeyDeletionPayloadEventIsCalledOnce()
    {
        // Arrange
        const string key = "pubsub:delete";

        await using var mimoriaClient = await this.ConnectToServerAsync();

        int calledCount = 0;
        string? receivedPayload = null;

        Subscription subscription = await mimoriaClient.SubscribeAsync(Channels.KeyDeletion);
        subscription.Payload += (deletedKey) =>
        {
            receivedPayload = deletedKey;
            calledCount++;
        };

        // Act
        await mimoriaClient.SetStringAsync(key, "value");
        await mimoriaClient.DeleteAsync(key);
        await mimoriaClient.DeleteAsync(key);

        await Task.Delay(500);

        // Assert
        Assert.Equal(key, receivedPayload);
        Assert.Equal(1, calledCount);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(20, 20)]
    [InlineData(30, 30)]
    [InlineData(40, 80)]
    public async Task PubSub_Given_MultipleClients_Concurrent_AddListAsync_Then_EverythingIsReceivedCorrectly(int clientCount, int itemCount)
    {
        // Arrange
        const string channel = "pubsub:list";

        var payloads = new List<string>(capacity: itemCount);
        for (int i = 0; i < itemCount; i++)
        {
            payloads.Add($"item{i}");
        }

        var clients = new List<IMimoriaClient>();
        for (int i = 0; i < clientCount; i++)
        {
            clients.Add(await this.ConnectToServerAsync());
        }

        await using var listenerClient = await this.ConnectToServerAsync();

        var receivedPayloads = new List<string>(capacity: clients.Count * payloads.Count);

        // Act
        Subscription subscription = await listenerClient.SubscribeAsync(Channels.ForListAdded(channel));
        subscription.Payload += (payload) =>
        {
            string item = payload!;

            lock (receivedPayloads)
            {
                receivedPayloads.Add(item);
            }
        };

        var tasks = new List<Task>(capacity: clients.Count);
        
        foreach (var client in clients)
        {
            tasks.Add(Task.Run(async () =>
            {
                foreach (var payload in payloads)
                {
                    await client.AddListAsync(channel, payload);
                }
            }));
        }

        await Task.WhenAll(tasks);

        await Task.Delay(500);

        // Assert
        Assert.Equal(payloads.Count * clients.Count, receivedPayloads.Count);
        foreach (var payload in payloads)
        {
            Assert.Equal(clients.Count, receivedPayloads.Count(receivedPayload => receivedPayload == payload));
        }

        // Cleanup
        foreach (var client in clients)
        {
            await client.DisconnectAsync();
        }
    }
}
