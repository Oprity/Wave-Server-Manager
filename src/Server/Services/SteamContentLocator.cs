// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace WaveServerManager.Services;

/// <summary>
/// Locates Steam installations and the Assetto Corsa game folder (appid 244210)
/// without any registry of our own: Windows registry + well-known paths for the
/// Steam client, then each library's <c>steamapps/libraryfolders.vdf</c>.
/// All filesystem roots are parameters so the logic is unit-testable.
/// </summary>
public static partial class SteamContentLocator
{
    public const int AssettoCorsaAppId = 244210;
    public const string AssettoCorsaFolderName = "assettocorsa";

    public static string SteamInstallUrl => $"steam://install/{AssettoCorsaAppId}";
    public static string SteamStoreUrl => $"https://store.steampowered.com/app/{AssettoCorsaAppId}/";

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex("\"\\d+\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyLibraryRegex();

    /// <summary>Finds Steam client installs on this machine.</summary>
    public static IReadOnlyList<string> FindSteamInstalls()
    {
        var found = new List<string>();

        foreach (var fromRegistry in ReadSteamPathsFromRegistry())
        {
            AddIfDirectory(found, fromRegistry);
        }

        if (OperatingSystem.IsWindows())
        {
            AddIfDirectory(found, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            AddIfDirectory(found, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddIfDirectory(found, Path.Combine(home, ".steam", "steam"));
            AddIfDirectory(found, Path.Combine(home, ".local", "share", "Steam"));
        }

        return found;
    }

    /// <summary>
    /// All game libraries for one Steam install: the install dir itself plus every
    /// library listed in <c>steamapps/libraryfolders.vdf</c> (new and legacy formats).
    /// </summary>
    public static IReadOnlyList<string> FindLibraries(string steamDir)
    {
        var libs = new List<string> { steamDir };

        var vdf = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
        {
            return libs;
        }

        string text;
        try
        {
            text = File.ReadAllText(vdf);
        }
        catch
        {
            return libs;
        }

        var matches = LibraryPathRegex().Matches(text);
        if (matches.Count == 0)
        {
            matches = LegacyLibraryRegex().Matches(text);
        }

        foreach (Match m in matches)
        {
            // VDF escapes backslashes.
            var path = m.Groups[1].Value.Replace("\\\\", "\\").Trim();
            if (Path.IsPathRooted(path))
            {
                AddIfDirectory(libs, path);
            }
        }

        return libs;
    }

    /// <summary>First library containing a valid Assetto Corsa folder, or null.</summary>
    public static string? FindAssettoCorsa(IEnumerable<string> libraryDirs)
    {
        foreach (var lib in libraryDirs)
        {
            var candidate = Path.Combine(lib, "steamapps", "common", AssettoCorsaFolderName);
            if (IsAssettoCorsaFolder(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    /// <summary>
    /// A folder counts as the game if it exists and holds the game binary or its
    /// content tree. Both spellings of the exe are accepted (case differs by install).
    /// </summary>
    public static bool IsAssettoCorsaFolder(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return false;
        }

        return File.Exists(Path.Combine(dir, "AssettoCorsa.exe"))
            || File.Exists(Path.Combine(dir, "assettocorsa.exe"))
            || Directory.Exists(Path.Combine(dir, "content"));
    }

    private static IEnumerable<string> ReadSteamPathsFromRegistry()
    {
        var results = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            return results;
        }

        try
        {
            var hkcu = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
            if (!string.IsNullOrWhiteSpace(hkcu))
            {
                results.Add(hkcu);
            }

            var hklm = Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
            if (!string.IsNullOrWhiteSpace(hklm))
            {
                results.Add(hklm);
            }
        }
        catch
        {
            // Registry locked down: fall back to well-known paths.
        }

        return results;
    }

    private static void AddIfDirectory(List<string> list, string dir)
    {
        string full;
        try
        {
            full = Path.GetFullPath(dir);
        }
        catch
        {
            return;
        }

        if (Directory.Exists(full) && !list.Contains(full, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(full);
        }
    }
}
