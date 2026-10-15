// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using AcServerManager.Logging;
using Microsoft.Extensions.Logging;

namespace AcServerManager.Tests;

public sealed class FileLoggingTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"acsm-logtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void TodayFilePath_Uses_Daily_Wave_FileName()
    {
        var path = LogFiles.TodayFilePath(NewTempDir());
        Assert.Equal($"wave-{DateTime.Now:yyyy-MM-dd}.log", Path.GetFileName(path));
    }

    [Fact]
    public void AppendLine_Creates_File_And_Appends()
    {
        var dir = NewTempDir();
        LogFiles.AppendLine("first", dir);
        LogFiles.AppendLine("second", dir);

        var text = File.ReadAllText(LogFiles.TodayFilePath(dir));
        Assert.Contains("first", text);
        Assert.Contains("second", text);
    }

    [Fact]
    public void PruneOldFiles_Deletes_Logs_Older_Than_7_Days_Only()
    {
        var dir = NewTempDir();
        var oldLog = Path.Combine(dir, "wave-2000-01-01.log");
        var freshLog = Path.Combine(dir, "wave-2000-01-02.log");
        var notes = Path.Combine(dir, "notes.txt");
        File.WriteAllText(oldLog, "old");
        File.WriteAllText(freshLog, "fresh");
        File.WriteAllText(notes, "keep");
        File.SetLastWriteTimeUtc(oldLog, DateTime.UtcNow.AddDays(-8));
        File.SetLastWriteTimeUtc(freshLog, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(notes, DateTime.UtcNow.AddDays(-30));

        LogFiles.PruneOldFiles(directory: dir);

        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(freshLog));
        Assert.True(File.Exists(notes));
    }

    [Fact]
    public void FileLoggerProvider_Writes_Information_But_Not_Debug()
    {
        var dir = NewTempDir();
        using var provider = new FileLoggerProvider(dir);
        var logger = provider.CreateLogger("Test.Category");

        logger.LogInformation("hello info");
        logger.LogDebug("quiet debug");

        var text = File.ReadAllText(LogFiles.TodayFilePath(dir));
        Assert.Contains("hello info", text);
        Assert.Contains("Test.Category", text);
        Assert.DoesNotContain("quiet debug", text);
    }
}
