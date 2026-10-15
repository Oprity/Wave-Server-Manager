// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace WaveServerManager.Logging;

/// <summary>
/// Shared rolling-file-log locations. Both the backend file sink and the desktop
/// host write to the same daily file so a whole session reads top-to-bottom.
/// </summary>
public static class LogFiles
{
    public const int DefaultRetainedDays = 7;

    private static readonly object Gate = new();

    /// <summary>%LocalAppData%\WaveServerManager\logs (or the platform equivalent).</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaveServerManager",
        "logs");

    public static string TodayFilePath(string? directory = null) =>
        Path.Combine(directory ?? DefaultDirectory, $"wave-{DateTime.Now:yyyy-MM-dd}.log");

    /// <summary>Appends one timestamped line to today's log (creates the folder).</summary>
    public static void AppendLine(string message, string? directory = null)
    {
        var dir = directory ?? DefaultDirectory;
        Directory.CreateDirectory(dir);
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
        lock (Gate)
        {
            File.AppendAllText(TodayFilePath(dir), line);
        }
    }

    /// <summary>Deletes *.log files (by last-write time) older than the retention window.</summary>
    public static void PruneOldFiles(int retainedDays = DefaultRetainedDays, string? directory = null)
    {
        var dir = directory ?? DefaultDirectory;
        if (!Directory.Exists(dir))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.Date.AddDays(-retainedDays);
        string[] files;
        try
        {
            files = Directory.GetFiles(dir, "*.log");
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Best effort: a locked or unreadable file must not break startup.
            }
        }
    }
}
