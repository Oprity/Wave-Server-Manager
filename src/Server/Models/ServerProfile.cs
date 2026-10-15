// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace AcServerManager.Models;

/// <summary>
/// A named Assetto Corsa server configuration profile.
/// Structured fields map to <c>server_cfg.ini</c> / <c>entry_list.ini</c>
/// via <see cref="Services.ProfileIniMapper"/>; unknown INI keys are preserved on disk.
/// </summary>
public sealed class ServerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name in the manager UI (not the in-game server name).</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = "My server";

    /// <summary>In-game server name (NAME in [SERVER]).</summary>
    [Required, MaxLength(100)]
    public string ServerName { get; set; } = "My AC Server";

    [Range(1, 65535)]
    public int TcpPort { get; set; } = 9600;

    [Range(1, 65535)]
    public int UdpPort { get; set; } = 9600;

    [Range(1, 65535)]
    public int HttpPort { get; set; } = 8081;

    [Range(0, 48)]
    public int MaxClients { get; set; } = 18;

    [MaxLength(64)]
    public string? Password { get; set; }

    [MaxLength(64)]
    public string? AdminPassword { get; set; }

    /// <summary>Content folder name, e.g. "ks_nurburgring".</summary>
    [Required, MaxLength(100)]
    public string Track { get; set; } = "ks_nurburgring";

    [MaxLength(100)]
    public string? TrackLayout { get; set; }

    /// <summary>Comma-separated car folder names, e.g. "ks_porsche_911_gt3_rs,ks_ferrari_488_gt3".</summary>
    [MaxLength(4000)]
    public string CarsCsv { get; set; } = "ks_porsche_911_gt3_rs";

    public bool PickupModeEnabled { get; set; } = true;
    public bool LoopMode { get; set; } = true;
    public bool AutoRestartOnCrash { get; set; } = false;

    /// <summary>
    /// Optional per-profile override of the acServer/AssettoServer executable.
    /// When null, the global <c>AcManager:AssettoServerExe</c> setting is used.
    /// </summary>
    [MaxLength(1024)]
    public string? ExecutablePath { get; set; }

    /// <summary>
    /// Optional CLI arguments appended after the executable (e.g. AssettoServer flags).
    /// Stock acServer needs none (it reads server_cfg.ini from the working directory).
    /// </summary>
    [MaxLength(1024)]
    public string? ExecutableArgs { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public string[] GetCars() => CarsCsv
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(c => c.Length > 0)
        .ToArray();
}
