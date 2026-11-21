// ILogger provider that writes to the debugger output (Visual Studio "Output" window), Debug builds only.

using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BrickController2.Windows.Diagnostics;

/// <summary>
/// The MAUI host registers no logging provider by default, so every <c>ILogger</c> call of BC2 (for example the
/// "USB stick: ..." warnings) would be discarded. In Debug builds this provider forwards them to
/// <see cref="Debug.WriteLine(string)"/> with the prefix "[ILOGGER]". It is registered in <c>App.CreateMauiApp</c>
/// inside <c>#if DEBUG</c>, so Release builds are unaffected.
/// </summary>
internal sealed class DebugOutputLoggerProvider : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new DebugOutputLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        // nothing to release
    }

    /// <summary>Logger of one category; formats "[ILOGGER] time level category: message".</summary>
    private sealed class DebugOutputLogger : ILogger
    {
        private readonly string _category;

        public DebugOutputLogger(string category) => _category = category;

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            Debug.WriteLine($"[ILOGGER] {DateTime.Now:HH:mm:ss.fff} {logLevel} {_category}: {formatter(state, exception)}{(exception != null ? " | " + exception : string.Empty)}");
        }
    }
}
