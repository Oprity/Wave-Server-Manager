// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using WaveServerManager.Ini;
using WaveServerManager.Models;

namespace WaveServerManager.Services;

/// <summary>
/// Maps <see cref="ServerProfile"/> structured fields to Assetto Corsa INI files.
/// Existing file content can be passed in so unknown keys/comments survive edits.
/// </summary>
public sealed class ProfileIniMapper
{
    public string ToServerCfgIni(ServerProfile p, string? existing = null)
    {
        var doc = string.IsNullOrEmpty(existing)
            ? DefaultServerCfgSkeleton()
            : IniDocument.Parse(existing);

        doc.SetValue("SERVER", "NAME", p.ServerName);
        doc.SetValue("SERVER", "CARS", string.Join(",", p.GetCars()));
        doc.SetValue("SERVER", "TRACK", p.Track);
        doc.SetValue("SERVER", "CONFIG_TRACK", p.TrackLayout ?? string.Empty);
        doc.SetValue("SERVER", "UDP_PORT", p.UdpPort.ToString());
        doc.SetValue("SERVER", "TCP_PORT", p.TcpPort.ToString());
        doc.SetValue("SERVER", "HTTP_PORT", p.HttpPort.ToString());
        doc.SetValue("SERVER", "MAX_CLIENTS", p.MaxClients.ToString());
        doc.SetValue("SERVER", "PASSWORD", p.Password ?? string.Empty);
        doc.SetValue("SERVER", "ADMIN_PASSWORD", p.AdminPassword ?? string.Empty);
        doc.SetValue("SERVER", "PICKUP_MODE_ENABLED", p.PickupModeEnabled ? "1" : "0");
        doc.SetValue("SERVER", "LOOP_MODE", p.LoopMode ? "1" : "0");

        return doc.Serialize();
    }

    public string ToEntryListIni(ServerProfile p, string? existing = null)
    {
        var cars = p.GetCars();
        IniDocument? existingDoc = string.IsNullOrEmpty(existing) ? null : IniDocument.Parse(existing);

        // Preserve per-slot customizations (SKIN, DRIVERNAME, ...) when the MODEL matches.
        var preserved = new Dictionary<int, Dictionary<string, string>>();
        if (existingDoc is not null)
        {
            foreach (var line in existingDoc.Lines.OfType<KeyLine>())
            {
                if (line.Section.StartsWith("CAR_", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Section.Substring(4), out var slot))
                {
                    if (!preserved.TryGetValue(slot, out var dict))
                    {
                        dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        preserved[slot] = dict;
                    }
                    dict[line.Key] = line.Value;
                }
            }
        }

        var doc = new IniDocument();
        // Seed empty doc with one blank-free structure via SetValue ordering.
        for (var i = 0; i < cars.Length; i++)
        {
            var section = $"CAR_{i}";
            string Skin() => preserved.TryGetValue(i, out var d) &&
                d.TryGetValue("MODEL", out var m) &&
                string.Equals(m, cars[i], StringComparison.OrdinalIgnoreCase) &&
                d.TryGetValue("SKIN", out var s) ? s : string.Empty;
            string Driver() => preserved.TryGetValue(i, out var d) &&
                d.TryGetValue("MODEL", out var m) &&
                string.Equals(m, cars[i], StringComparison.OrdinalIgnoreCase) &&
                d.TryGetValue("DRIVERNAME", out var s) ? s : string.Empty;

            doc.SetValue(section, "MODEL", cars[i]);
            doc.SetValue(section, "SKIN", Skin());
            doc.SetValue(section, "SPECTATOR_MODE", "0");
            doc.SetValue(section, "DRIVERNAME", Driver());
            doc.SetValue(section, "TEAM", string.Empty);
            doc.SetValue(section, "GUID", string.Empty);
        }

        return doc.Serialize();
    }

    private static IniDocument DefaultServerCfgSkeleton() => IniDocument.Parse(
        """
        ; Assetto Corsa server_cfg.ini managed by Wave Server Manager.
        ; Unknown keys and comments are preserved on round-trip.

        [SERVER]
        NAME=My AC Server
        CARS=ks_porsche_911_gt3_rs
        CONFIG_TRACK=
        TRACK=ks_nurburgring
        SUN_ANGLE=48
        UDP_PORT=9600
        TCP_PORT=9600
        HTTP_PORT=8081
        MAX_CLIENTS=18
        PICKUP_MODE_ENABLED=1
        LOOP_MODE=1
        PASSWORD=
        ADMIN_PASSWORD=
        REGISTER_TO_LOBBY=0
        """);
}
