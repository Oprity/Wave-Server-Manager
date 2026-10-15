// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;

namespace WaveServerManager.Services;

public sealed record ContentStatus(
    IReadOnlyList<string> SteamInstalls,
    IReadOnlyList<string> Libraries,
    string? AutoDetectedPath,
    string? ManualPath,
    string? EffectivePath,
    bool Found,
    string SteamInstallUrl,
    string SteamStoreUrl);

/// <summary>
/// Resolves the Assetto Corsa game folder: an explicit manual override
/// (<c>content.json</c> in the data dir) wins, otherwise Steam auto-detection.
/// </summary>
public sealed class ContentPathsService
{
    private readonly string _settingsFile;

    public ContentPathsService(AcPathsService paths)
    {
        _settingsFile = Path.Combine(paths.DataDir, "content.json");
    }

    public ContentStatus GetStatus()
    {
        var installs = SteamContentLocator.FindSteamInstalls();
        var libraries = installs
            .SelectMany(SteamContentLocator.FindLibraries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var auto = SteamContentLocator.FindAssettoCorsa(libraries);
        var manual = ReadManualPath();
        if (manual is not null && !SteamContentLocator.IsAssettoCorsaFolder(manual))
        {
            manual = null; // Override went stale (game moved/uninstalled): ignore it.
        }

        var effective = manual ?? auto;
        return new ContentStatus(
            installs, libraries, auto, manual, effective, effective is not null,
            SteamContentLocator.SteamInstallUrl, SteamContentLocator.SteamStoreUrl);
    }

    public string? GetManualPath() => ReadManualPath();

    /// <summary>Validates and persists a user-supplied game folder.</summary>
    public (bool Ok, string? Error) SetManualPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, "Path must not be empty.");
        }

        var trimmed = path.Trim().Trim('"');
        if (!Path.IsPathRooted(trimmed))
        {
            return (false, "Enter a full folder path, e.g. C:\\Program Files (x86)\\Steam\\steamapps\\common\\assettocorsa.");
        }

        string full;
        try
        {
            full = Path.GetFullPath(trimmed);
        }
        catch
        {
            return (false, "That path is not valid.");
        }

        if (!Directory.Exists(full))
        {
            return (false, "That folder does not exist.");
        }

        if (!SteamContentLocator.IsAssettoCorsaFolder(full))
        {
            return (false, "That folder does not look like Assetto Corsa (expected AssettoCorsa.exe or a content folder inside).");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(_settingsFile, JsonSerializer.Serialize(new ContentSettings(full)));
        }
        catch (Exception ex)
        {
            return (false, $"Could not save the setting: {ex.Message}");
        }

        return (true, null);
    }

    public void ClearManualPath()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                File.Delete(_settingsFile);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private string? ReadManualPath()
    {
        try
        {
            if (!File.Exists(_settingsFile))
            {
                return null;
            }
            var settings = JsonSerializer.Deserialize<ContentSettings>(File.ReadAllText(_settingsFile));
            return string.IsNullOrWhiteSpace(settings?.AssettoCorsaPath) ? null : settings.AssettoCorsaPath;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ContentSettings(string AssettoCorsaPath);
}
