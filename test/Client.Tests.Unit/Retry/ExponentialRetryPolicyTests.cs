// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using FluentAssertions;

using Varelen.Mimoria.Client.Retry;

namespace Varelen.Mimoria.Client.Tests.Unit.Retry;

public class ExponentialRetryPolicyTests
{
    [Theory]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(NullReferenceException), false)]
    public void IsTransient_ShouldMatchPassedTransientExceptionType(Type transientExceptionType, bool result)
    {
        // Arrange
        var policy = new ExponentialRetryPolicy(10, 3, typeof(ArgumentException));

        // Act
        bool transient = policy.IsTransient(transientExceptionType);

        // Assert
        transient.Should().Be(result);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 400)]
    [InlineData(4, 800)]
    [InlineData(5, 1600)]
    public void GetDelay_ShouldIncreaseExponential(byte currentRetry, int result)
    {
        // Arrange
        var policy = new ExponentialRetryPolicy(100, 3, typeof(ArgumentException));

        // Act
        int delay = policy.GetDelay(currentRetry);

        // Assert
        delay.Should().Be(result);
    }
}
