// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Configuration;
using WaveServerManager.Services;

namespace WaveServerManager.Tests;

public sealed class ContentDetectionTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"wave-content-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static ContentPathsService NewService(string dataDir)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AcManager:DataDir"] = dataDir })
            .Build();
        return new ContentPathsService(new AcPathsService(config));
    }

    private static string MakeFakeGame(string libraryDir)
    {
        var game = Path.Combine(libraryDir, "steamapps", "common", "assettocorsa");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "AssettoCorsa.exe"), "fake");
        return game;
    }

    private static string MakeFakeSteam(string root, string? extraLibrary = null)
    {
        var steamDir = Path.Combine(root, "Steam");
        Directory.CreateDirectory(Path.Combine(steamDir, "steamapps"));
        var vdf = extraLibrary is null
            ? """
              "libraryfolders"
              {
              	"0"
              	{
              		"path"		"STEAMDIR"
              		"label"		""
              	}
              }
              """.Replace("STEAMDIR", steamDir.Replace("\\", "\\\\"))
            : """
              "libraryfolders"
              {
              	"0"
              	{
              		"path"		"STEAMDIR"
              	}
              	"1"
              	{
              		"path"		"EXTRADIR"
              	}
              }
              """.Replace("STEAMDIR", steamDir.Replace("\\", "\\\\"))
                .Replace("EXTRADIR", extraLibrary.Replace("\\", "\\\\"));
        File.WriteAllText(Path.Combine(steamDir, "steamapps", "libraryfolders.vdf"), vdf);
        return steamDir;
    }

    [Fact]
    public void FindLibraries_Parses_New_Format_Vdf()
    {
        var root = NewTempDir();
        var extra = Path.Combine(root, "ExtraLib");
        Directory.CreateDirectory(extra);
        var steamDir = MakeFakeSteam(root, extra);

        var libs = SteamContentLocator.FindLibraries(steamDir);

        Assert.Contains(libs, l => l.Equals(steamDir, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(libs, l => l.Equals(extra, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindLibraries_Falls_Back_To_Steam_Dir_Without_Vdf()
    {
        var steamDir = Path.Combine(NewTempDir(), "Steam");
        Directory.CreateDirectory(Path.Combine(steamDir, "steamapps"));

        var libs = SteamContentLocator.FindLibraries(steamDir);

        Assert.Single(libs);
    }

    [Fact]
    public void FindAssettoCorsa_Locates_Game_In_Extra_Library()
    {
        var root = NewTempDir();
        var extra = Path.Combine(root, "ExtraLib");
        Directory.CreateDirectory(extra);
        var steamDir = MakeFakeSteam(root, extra);
        var game = MakeFakeGame(extra);

        var found = SteamContentLocator.FindAssettoCorsa(SteamContentLocator.FindLibraries(steamDir));

        Assert.Equal(game, found);
    }

    [Fact]
    public void IsAssettoCorsaFolder_Rejects_Empty_Directory()
    {
        var dir = Path.Combine(NewTempDir(), "empty");
        Directory.CreateDirectory(dir);

        Assert.False(SteamContentLocator.IsAssettoCorsaFolder(dir));
        Assert.False(SteamContentLocator.IsAssettoCorsaFolder(Path.Combine(NewTempDir(), "missing")));
    }

    [Fact]
    public void SetManualPath_Accepts_Valid_And_Rejects_Bad_Folders()
    {
        var svc = NewService(NewTempDir());
        var game = MakeFakeGame(NewTempDir());

        var (ok, error) = svc.SetManualPath(game);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(game, svc.GetManualPath());

        var empty = Path.Combine(NewTempDir(), "empty");
        Directory.CreateDirectory(empty);
        var (ok2, error2) = svc.SetManualPath(empty);
        Assert.False(ok2);
        Assert.NotNull(error2);

        var (ok3, _) = svc.SetManualPath("relative/path");
        Assert.False(ok3);
    }

    [Fact]
    public void ClearManualPath_Falls_Back_To_AutoDetect()
    {
        var svc = NewService(NewTempDir());
        var game = MakeFakeGame(NewTempDir());
        svc.SetManualPath(game);
        Assert.True(svc.GetStatus().Found);

        svc.ClearManualPath();
        var status = svc.GetStatus();
        Assert.Null(status.ManualPath);
    }

    [Fact]
    public void GetStatus_Reports_Steam_Urls()
    {
        var status = NewService(NewTempDir()).GetStatus();
        Assert.Equal("steam://install/244210", status.SteamInstallUrl);
        Assert.Contains("store.steampowered.com", status.SteamStoreUrl);
    }
}
