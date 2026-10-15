// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace WaveServerManager.Services;

/// <summary>
/// Resolves and creates the app's on-disk folders (Milestone 1, feature 1:
/// "create an Assetto Corsa folder for the user somewhere clear and safe").
/// Windows: %AppData%\WaveServerManager · Linux/macOS: ~/.local/share/wave-server-manager.
/// Overridable via appsettings <c>AcManager:DataDir</c>.
/// </summary>
public sealed class AcPathsService
{
    private readonly string _dataDir;

    public AcPathsService(IConfiguration config)
    {
        var configured = config.GetValue<string>("AcManager:DataDir");
        _dataDir = string.IsNullOrWhiteSpace(configured) ? DefaultDataDir() : configured;
    }

    public string DataDir => _dataDir;
    public string ProfilesDir => Path.Combine(_dataDir, "profiles");
    public string LogsDir => Path.Combine(_dataDir, "logs");

    public static string DefaultDataDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "WaveServerManager");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "wave-server-manager");
    }

    /// <summary>Creates all folders (idempotent). Called at startup.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(LogsDir);
    }

    /// <summary>Folder for a single profile's cfg/entry_list INI files.</summary>
    public string ProfileDir(Guid profileId)
    {
        var dir = PathGuards.EnsureWithinBase(ProfilesDir, profileId.ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
