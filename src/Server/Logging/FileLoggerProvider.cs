// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace WaveServerManager.Logging;

/// <summary>
/// Minimal <see cref="ILoggerProvider"/> writing Information+ lines to the daily
/// rolling file in <see cref="LogFiles.DefaultDirectory"/>. Deliberately tiny
/// (no external sink package) so the single-file desktop publish stays lean.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;

    public FileLoggerProvider(string? directory = null)
    {
        _directory = directory ?? LogFiles.DefaultDirectory;
        LogFiles.PruneOldFiles(directory: _directory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_directory, categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger(string directory, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception is not null)
            {
                message += Environment.NewLine + exception;
            }

            LogFiles.AppendLine($"[{logLevel}] {category}: {message}", directory);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}

public static class FileLoggingExtensions
{
    public static ILoggingBuilder AddAppFileLogging(this ILoggingBuilder builder, string? directory = null)
    {
        builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(directory));
        return builder;
    }
}
