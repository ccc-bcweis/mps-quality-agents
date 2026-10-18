// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace Mps.Api.IntegrationTests.Support;

/// <summary>
/// Writes the server's warnings and errors, with their exceptions, to the running test's output. When an endpoint
/// answers 500 in CI, the failing test then shows the server stack instead of only the status code.
/// The integration tests run one at a time (AssemblyInfo), so one current output is enough.
/// </summary>
public sealed class TestOutputLoggerProvider : ILoggerProvider
{
    private readonly object _gate = new();
    private ITestOutputHelper? _output;

    /// <summary>Points the server log at the test that is about to run.</summary>
    public void Attach(ITestOutputHelper output)
    {
        lock (_gate)
        {
            _output = output;
        }
    }

    public ILogger CreateLogger(string categoryName) => new OutputLogger(this, categoryName);

    public void Dispose()
    {
        lock (_gate)
        {
            _output = null;
        }
    }

    private void Write(string line)
    {
        lock (_gate)
        {
            try
            {
                _output?.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // The test that owned this output has finished; a late background log line has nowhere to go.
            }
        }
    }

    private sealed class OutputLogger(TestOutputLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"[server {logLevel}] {category}: {formatter(state, exception)}";
            provider.Write(exception is null ? line : line + Environment.NewLine + exception);
        }
    }
}
