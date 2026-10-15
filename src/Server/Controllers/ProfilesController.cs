// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using WaveServerManager.Data;
using WaveServerManager.Dtos;
using WaveServerManager.Models;
using WaveServerManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WaveServerManager.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServerProcessManager _processes;
    private readonly ProfileIniMapper _ini;

    public ProfilesController(AppDbContext db, IServerProcessManager processes, ProfileIniMapper ini)
    {
        _db = db;
        _processes = processes;
        _ini = ini;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServerProfile>>> List(CancellationToken ct)
    {
        var items = await _db.Profiles.OrderBy(p => p.Name).ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ServerProfile>> Get(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<ActionResult<ServerProfile>> Create([FromBody] CreateProfileDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var invalid = ValidateContentNames(dto);
        if (invalid is not null)
        {
            return BadRequest(new { error = invalid });
        }

        var p = new ServerProfile
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            ServerName = dto.ServerName.Trim(),
            TcpPort = dto.TcpPort,
            UdpPort = dto.UdpPort,
            HttpPort = dto.HttpPort,
            MaxClients = dto.MaxClients,
            Password = EmptyToNull(dto.Password),
            AdminPassword = EmptyToNull(dto.AdminPassword),
            Track = dto.Track.Trim(),
            TrackLayout = EmptyToNull(dto.TrackLayout)?.Trim(),
            CarsCsv = NormalizeCars(dto.CarsCsv),
            PickupModeEnabled = dto.PickupModeEnabled,
            LoopMode = dto.LoopMode,
            AutoRestartOnCrash = dto.AutoRestartOnCrash,
            ExecutablePath = EmptyToNull(dto.ExecutablePath)?.Trim(),
            ExecutableArgs = EmptyToNull(dto.ExecutableArgs)?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        _db.Profiles.Add(p);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ServerProfile>> Update(Guid id, [FromBody] UpdateProfileDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }

        var invalid = ValidateContentNames(dto);
        if (invalid is not null)
        {
            return BadRequest(new { error = invalid });
        }

        p.Name = dto.Name.Trim();
        p.ServerName = dto.ServerName.Trim();
        p.TcpPort = dto.TcpPort;
        p.UdpPort = dto.UdpPort;
        p.HttpPort = dto.HttpPort;
        p.MaxClients = dto.MaxClients;
        p.Password = EmptyToNull(dto.Password);
        p.AdminPassword = EmptyToNull(dto.AdminPassword);
        p.Track = dto.Track.Trim();
        p.TrackLayout = EmptyToNull(dto.TrackLayout)?.Trim();
        p.CarsCsv = NormalizeCars(dto.CarsCsv);
        p.PickupModeEnabled = dto.PickupModeEnabled;
        p.LoopMode = dto.LoopMode;
        p.AutoRestartOnCrash = dto.AutoRestartOnCrash;
        p.ExecutablePath = EmptyToNull(dto.ExecutablePath)?.Trim();
        p.ExecutableArgs = EmptyToNull(dto.ExecutableArgs)?.Trim();
        p.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(p);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }

        await _processes.StopAsync(id, ct);
        _db.Profiles.Remove(p);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<ServerProfile>> Duplicate(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }

        var copy = new ServerProfile
        {
            Id = Guid.NewGuid(),
            Name = Truncate($"{p.Name} (copy)", 100),
            ServerName = Truncate($"{p.ServerName} (copy)", 100),
            TcpPort = p.TcpPort,
            UdpPort = p.UdpPort,
            HttpPort = p.HttpPort,
            MaxClients = p.MaxClients,
            Password = p.Password,
            AdminPassword = p.AdminPassword,
            Track = p.Track,
            TrackLayout = p.TrackLayout,
            CarsCsv = p.CarsCsv,
            PickupModeEnabled = p.PickupModeEnabled,
            LoopMode = p.LoopMode,
            AutoRestartOnCrash = p.AutoRestartOnCrash,
            ExecutablePath = p.ExecutablePath,
            ExecutableArgs = p.ExecutableArgs,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        _db.Profiles.Add(copy);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = copy.Id }, copy);
    }

    [HttpGet("{id:guid}/server-cfg")]
    public async Task<ActionResult> GetServerCfg(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }
        return Content(_ini.ToServerCfgIni(p), "text/plain");
    }

    [HttpGet("{id:guid}/entry-list")]
    public async Task<ActionResult> GetEntryList(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }
        return Content(_ini.ToEntryListIni(p), "text/plain");
    }

    [HttpGet("{id:guid}/status")]
    public ActionResult<ServerStatusDto> Status(Guid id) => Ok(_processes.GetStatus(id));

    [HttpGet("{id:guid}/logs")]
    public async Task<ActionResult> RecentLogs(Guid id, CancellationToken ct)
    {
        if (!await _db.Profiles.AnyAsync(p => p.Id == id, ct))
        {
            return NotFound();
        }
        return Ok(_processes.GetRecentLogs(id));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ServerStatusDto>> Start(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }

        try
        {
            return Ok(await _processes.StartAsync(p, ct));
        }
        catch (FileNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<ActionResult<ServerStatusDto>> Stop(Guid id, CancellationToken ct)
    {
        if (!await _db.Profiles.AnyAsync(p => p.Id == id, ct))
        {
            return NotFound();
        }

        await _processes.StopAsync(id, ct);
        return Ok(_processes.GetStatus(id));
    }

    [HttpPost("{id:guid}/restart")]
    public async Task<ActionResult<ServerStatusDto>> Restart(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.FindAsync(new object[] { id }, ct);
        if (p is null)
        {
            return NotFound();
        }

        try
        {
            await _processes.StopAsync(id, ct);
            return Ok(await _processes.StartAsync(p, ct));
        }
        catch (FileNotFoundException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static string? ValidateContentNames(CreateProfileDto dto)
    {
        if (!ProfileRules.IsValidContentName(dto.Track.Trim()))
        {
            return "Track must be a content folder name (letters, digits, '_' or '-').";
        }

        foreach (var car in dto.CarsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ProfileRules.IsValidContentName(car))
            {
                return $"Invalid car folder name: '{car}'. Use letters, digits, '_' or '-'.";
            }
        }

        if (dto.TcpPort == dto.UdpPort || dto.TcpPort == dto.HttpPort || dto.UdpPort == dto.HttpPort)
        {
            // TCP/UDP share 9600 by AC convention historically, so only warn on HTTP collisions.
            if (dto.HttpPort == dto.TcpPort || dto.HttpPort == dto.UdpPort)
            {
                return "HTTP port must differ from TCP/UDP ports.";
            }
        }

        return null;
    }

    private static string NormalizeCars(string csv) =>
        string.Join(",", csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? EmptyToNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;

    private static string Truncate(string v, int max) => v.Length <= max ? v : v.Substring(0, max);
}
