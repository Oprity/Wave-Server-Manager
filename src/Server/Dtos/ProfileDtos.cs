// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AcServerManager.Dtos;

/// <summary>Shared validation rules for profile fields (also enforced client-side).</summary>
public static partial class ProfileRules
{
    [GeneratedRegex(@"^[A-Za-z0-9_\-]+$", RegexOptions.Compiled)]
    private static partial Regex ContentNameRegex();

    public static bool IsValidContentName(string v) => ContentNameRegex().IsMatch(v);
}

public class CreateProfileDto
{
    [Required, MaxLength(100)] public string Name { get; set; } = "My server";
    [Required, MaxLength(100)] public string ServerName { get; set; } = "My AC Server";
    [Range(1, 65535)] public int TcpPort { get; set; } = 9600;
    [Range(1, 65535)] public int UdpPort { get; set; } = 9600;
    [Range(1, 65535)] public int HttpPort { get; set; } = 8081;
    [Range(0, 48)] public int MaxClients { get; set; } = 18;
    [MaxLength(64)] public string? Password { get; set; }
    [MaxLength(64)] public string? AdminPassword { get; set; }
    [Required, MaxLength(100)] public string Track { get; set; } = "ks_nurburgring";
    [MaxLength(100)] public string? TrackLayout { get; set; }
    [MaxLength(4000)] public string CarsCsv { get; set; } = "ks_porsche_911_gt3_rs";
    public bool PickupModeEnabled { get; set; } = true;
    public bool LoopMode { get; set; } = true;
    public bool AutoRestartOnCrash { get; set; } = false;
    [MaxLength(1024)] public string? ExecutablePath { get; set; }
    [MaxLength(1024)] public string? ExecutableArgs { get; set; }
}

public sealed class UpdateProfileDto : CreateProfileDto
{
}
