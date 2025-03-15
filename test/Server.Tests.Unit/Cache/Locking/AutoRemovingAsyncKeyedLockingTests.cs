// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Varelen.Mimoria.Server.Cache.Locking;

namespace Varelen.Mimoria.Server.Tests.Unit.Cache.Locking;

public class AutoRemovingAsyncKeyedLockingTests
{
    private readonly AutoRemovingAsyncKeyedLocking sut;

    public AutoRemovingAsyncKeyedLockingTests()
    {
        this.sut = new AutoRemovingAsyncKeyedLocking(initialCapacity: 1);
    }

    [Fact]
    public async Task LockAsync_Given_TakeLockDefault_Then_ReleaserIsReturned()
    {
        // Arrange
        const string key = "key";

        // Act
        using var referenceCountedReleaser = await this.sut.LockAsync(key);

        // Assert
        Assert.NotNull(referenceCountedReleaser);
        Assert.True(this.sut.HasActiveLock(key));
    }

    [Fact]
    public async Task LockAsync_Given_TakeLockFalse_Then_NullIsReturned()
    {
        // Arrange
        const string key = "key";

        // Act
        using var referenceCountedReleaser = await this.sut.LockAsync(key, takeLock: false);

        // Assert
        Assert.Null(referenceCountedReleaser);
        Assert.False(this.sut.HasActiveLock(key));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task LockAsync_Given_SpecificLockAsyncCallsSuccessively_Then_LockIsTakenAndReleased(int iterations)
    {
        const string key = "key";

        for (int i = 0; i < iterations; i++)
        {
            Assert.False(this.sut.HasActiveLock(key));

            using ReferenceCountedReleaser? releaser = await this.sut.LockAsync(key);

            Assert.NotNull(releaser);
            Assert.True(this.sut.HasActiveLock(key));
        }
    }
}
