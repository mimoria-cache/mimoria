// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Varelen.Mimoria.Core.Buffer;
using Varelen.Mimoria.Server.Network;
using Varelen.Mimoria.Server.PubSub;

namespace Varelen.Mimoria.Server.Tests.Unit;

public class PubSubServiceTests
{
    [Fact]
    public async Task PublishAsync_When_SubscribingAndPublishing_Then_SendAsyncOfTcpConnectionIsInvokedAsync()
    {
        // Arrange
        const string channel = "test";
        const string payload = "payload";
        const int publishPacketSize = 19;

        using var sut = new PubSubService(NullLogger<PubSubService>.Instance);

        var tcpConnectionMock = Substitute.For<ITcpConnection>();
        tcpConnectionMock.Id.Returns<ulong>(5);

        // Act
        await sut.SubscribeAsync(channel, tcpConnectionMock);

        await sut.PublishAsync(channel, payload);

        // Assert
        await tcpConnectionMock.Received().SendAsync(Arg.Is<IByteBuffer>(b => b.Size == publishPacketSize));
    }

    [Fact]
    public async Task PublishAsync_When_SubscribingUnsubscribingAndPublishing_Then_SendAsyncOfTcpConnectionIsNotInvoked()
    {
        // Arrange
        const string channel = "test";
        const string payload = "payload";

        using var sut = new PubSubService(NullLogger<PubSubService>.Instance);

        var tcpConnectionMock = Substitute.For<ITcpConnection>();
        tcpConnectionMock.Id.Returns<ulong>(6);

        // Act
        await sut.SubscribeAsync(channel, tcpConnectionMock);
        await sut.UnsubscribeAsync(channel, tcpConnectionMock);

        await sut.PublishAsync(channel, payload);

        // Assert
        await tcpConnectionMock.DidNotReceive().SendAsync(Arg.Any<IByteBuffer>());
    }

    [Fact]
    public async Task PublishAsync_When_SubscribingUnsubscribingAllAndPublishing_Then_SendAsyncOfTcpConnectionIsNotInvoked()
    {
        // Arrange
        const string channel = "test";
        const string payload = "payload";

        using var sut = new PubSubService(NullLogger<PubSubService>.Instance);

        var tcpConnectionMock = Substitute.For<ITcpConnection>();
        tcpConnectionMock.Id.Returns<ulong>(6);

        // Act
        await sut.SubscribeAsync(channel, tcpConnectionMock);
        await sut.UnsubscribeAsync(tcpConnectionMock);

        await sut.PublishAsync(channel, payload);

        // Assert
        await tcpConnectionMock.DidNotReceive().SendAsync(Arg.Any<IByteBuffer>());
    }

    [Fact]
    public async Task Concurrent_SubscribePublishUnsubscribe_With_SubscribedAnUnsubscribedChannelAsync()
    {
        // Arrange
        using var sut = new PubSubService(NullLogger<PubSubService>.Instance);

        var t = Substitute.For<ITcpConnection>();
        t.Id.Returns<ulong>(7);

        const string channel = "test";
        const string notSubscribedChannel = "test2";
        const int taskCount = 10;
        const int iterationCount = 10_000;
        const ulong totalIterationCount = taskCount * iterationCount;

        int run = 0;
        ulong operations = 0;

        // Act
        var tasks = new List<Task>(capacity: taskCount);

        for (int i = 0; i < taskCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < iterationCount; i++)
                {
                    await sut.SubscribeAsync(channel, t);
                    await sut.PublishAsync(channel, "payload");
                    await sut.PublishAsync(notSubscribedChannel, "payload");
                    await sut.UnsubscribeAsync(channel, t);
                    await sut.UnsubscribeAsync(notSubscribedChannel, t);

                    Interlocked.Increment(ref operations);
                }

                Interlocked.Increment(ref run);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(taskCount, run);
        Assert.Equal(totalIterationCount, operations);
    }
}
