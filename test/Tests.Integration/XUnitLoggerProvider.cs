// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace Varelen.Mimoria.Tests.Integration;

public sealed class XUnitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper testOutputHelper;

    public XUnitLoggerProvider(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper
            ?? throw new ArgumentNullException(nameof(testOutputHelper));
    }

    public ILogger CreateLogger(string categoryName)
        => new XUnitLogger(this.testOutputHelper, categoryName);

    public void Dispose()
    {

    }

    private sealed class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly string categoryName;

        public XUnitLogger(ITestOutputHelper testOutputHelper, string categoryName)
        {
            this.testOutputHelper = testOutputHelper;
            this.categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!this.IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var logEntry = $"{logLevel}: {this.categoryName} - {message}";

            if (exception != null)
            {
                logEntry += Environment.NewLine + exception;
            }

            this.testOutputHelper.WriteLine(logEntry);
        }
    }
}
