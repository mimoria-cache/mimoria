// SPDX-FileCopyrightText: 2024 varelen
//
// SPDX-License-Identifier: MIT

using FluentAssertions;

using Varelen.Mimoria.Client.Retry;

namespace Varelen.Mimoria.Client.Tests.Unit.Retry;

public sealed class LinearRetryPolicyTests
{
    [Theory]
    [InlineData(typeof(ArgumentException), true)]
    [InlineData(typeof(ArgumentOutOfRangeException), true)]
    [InlineData(typeof(NullReferenceException), false)]
    public void IsTransient_ShouldMatchPassedTransientExceptionTypes(Type transientExceptionType, bool result)
    {
        // Arrange
        var policy = new LinearRetryPolicy(10, 3, typeof(ArgumentException), typeof(ArgumentOutOfRangeException));

        // Act
        bool transient = policy.IsTransient(transientExceptionType);

        // Assert
        transient.Should().Be(result);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 300)]
    [InlineData(4, 400)]
    public void GetDelay_ShouldIncreaseLinear(byte currentRetry, int result)
    {
        // Arrange
        var policy = new LinearRetryPolicy(100, 3, typeof(ArgumentException));

        // Act
        int delay = policy.GetDelay(currentRetry);

        // Assert
        delay.Should().Be(result);
    }
}
