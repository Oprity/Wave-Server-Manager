// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Mvc;
using WaveServerManager.Services;

namespace WaveServerManager.Controllers;

/// <summary>Local-first setup: data folders, binary detection, game content location.</summary>
[ApiController]
[Route("api/setup")]
public sealed class SetupController : ControllerBase
{
    private readonly AcPathsService _paths;
    private readonly IConfiguration _config;
    private readonly ContentPathsService _content;

    public SetupController(AcPathsService paths, IConfiguration config, ContentPathsService content)
    {
        _paths = paths;
        _config = config;
        _content = content;
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

    /// <summary>Steam installs, libraries and the resolved Assetto Corsa folder.</summary>
    [HttpGet("content")]
    public ActionResult<ContentStatus> GetContent() => Ok(_content.GetStatus());

    public sealed record SetContentPathDto(string? Path);

    /// <summary>Manually set the Assetto Corsa folder (validated server-side).</summary>
    [HttpPost("content")]
    public ActionResult<ContentStatus> SetContent([FromBody] SetContentPathDto dto)
    {
        var (ok, error) = _content.SetManualPath(dto.Path);
        if (!ok)
        {
            return BadRequest(new { error });
        }
        return Ok(_content.GetStatus());
    }

    /// <summary>Forget the manual override and go back to Steam auto-detection.</summary>
    [HttpDelete("content")]
    public ActionResult<ContentStatus> ClearContent()
    {
        _content.ClearManualPath();
        return Ok(_content.GetStatus());
    }
}
