// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using WaveServerManager.Hosting;

namespace WaveServerManager.Tests;

public sealed class AppHostTests
{
    [Fact]
    public async Task Create_Honors_Explicit_Content_Root_Without_Touching_Disk()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wave-apphost-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var dbName = $"apphost-{Guid.NewGuid():N}";

        await using var app = AppHost.Create(
            ["--Database:Provider", "InMemory", "--Database:ConnectionString", dbName],
            contentRootPath: root);

        Assert.Equal(root, app.Environment.ContentRootPath);
        Assert.False(File.Exists(Path.Combine(root, "wave-server-manager.db")));
    }
}
