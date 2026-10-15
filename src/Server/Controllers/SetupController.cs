// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using WaveServerManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace WaveServerManager.Controllers;

/// <summary>Local-first setup: data folders, binary detection (never bundled).</summary>
[ApiController]
[Route("api/setup")]
public sealed class SetupController : ControllerBase
{
    private readonly AcPathsService _paths;
    private readonly IConfiguration _config;

    public SetupController(AcPathsService paths, IConfiguration config)
    {
        _paths = paths;
        _config = config;
    }

    [HttpGet("paths")]
    public ActionResult GetPaths()
    {
        _paths.EnsureCreated();
        var exe = _config.GetValue<string>("AcManager:AssettoServerExe");
        return Ok(new
        {
            dataDir = _paths.DataDir,
            profilesDir = _paths.ProfilesDir,
            logsDir = _paths.LogsDir,
            assettoServerExe = exe,
            assettoServerExists = !string.IsNullOrWhiteSpace(exe) && System.IO.File.Exists(exe),
        });
    }
}
