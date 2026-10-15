// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace WaveServerManager.Services;

/// <summary>Guards against path traversal for user-supplied file names and sub-paths.</summary>
public static class PathGuards
{
    private static readonly char[] InvalidNameChars =
        System.IO.Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToArray();

    /// <summary>Validates a single path segment (profile folder name, car/track folder, ...).</summary>
    public static string RequireSafeSegment(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", paramName);
        }

        var v = value.Trim();
        if (v is "." or ".." || v.IndexOfAny(InvalidNameChars) >= 0 || v.Contains(".."))
        {
            throw new ArgumentException($"Unsafe path segment: '{value}'.", paramName);
        }

        return v;
    }

    /// <summary>Resolves <paramref name="userPath"/> against <paramref name="baseDir"/> and
    /// throws unless the result stays inside <paramref name="baseDir"/>.</summary>
    public static string EnsureWithinBase(string baseDir, string userPath)
    {
        var fullBase = Path.GetFullPath(baseDir);
        var full = Path.GetFullPath(Path.Combine(fullBase, userPath));
        if (!full.StartsWith(fullBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full, fullBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Path escapes the allowed base directory.");
        }
        return full;
    }
}
