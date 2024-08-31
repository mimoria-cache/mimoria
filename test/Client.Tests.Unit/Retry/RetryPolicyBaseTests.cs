// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using FluentAssertions;

using Varelen.Mimoria.Client.Retry;

namespace Varelen.Mimoria.Client.Tests.Unit.Retry;

public sealed class RetryPolicyBaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRetryUpToMaxRetries()
    {
        // Arrange
        const byte maxRetries = 4;
        IRetryPolicy policy = new StaticRetryPolicy(maxRetries);
        int executionCount = -1;

        // Act and Assert
        try
        {
            await policy.ExecuteAsync<bool>(() =>
            {
                executionCount++;
                throw new ArgumentException();
            });
            throw new InvalidOperationException("This should not be thrown");
        }
        catch (ArgumentException)
        {
            executionCount.Should().Be(maxRetries);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnIfFunctionDoesNotThrowExceptionAfterFewerRetries()
    {
        // Arrange
        const byte maxRetries = 4;
        IRetryPolicy policy = new StaticRetryPolicy(maxRetries);
        byte executionCount = 0;

        // Act
        bool returned = await policy.ExecuteAsync(() =>
        {
            executionCount++;
            if (executionCount == maxRetries - 1)
            {
                return Task.FromResult(true);
            }

            throw new ArgumentException();
        });

        // Assert
        returned.Should().BeTrue();
        executionCount.Should().Be(maxRetries - 1);
    }

    private class StaticRetryPolicy : RetryPolicyBase
    {
        public StaticRetryPolicy(byte maxRetries)
            : base(maxRetries, typeof(ArgumentException))
        {
        }

        public override int GetDelay(byte currentRetry)
            => 10;
    }
}
