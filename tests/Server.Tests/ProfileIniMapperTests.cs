// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using AcServerManager.Models;
using AcServerManager.Services;

namespace AcServerManager.Tests;

public sealed class ProfileIniMapperTests
{
    private readonly ProfileIniMapper _mapper = new();

    private static ServerProfile Sample() => new()
    {
        ServerName = "Wave Test",
        TcpPort = 9600,
        UdpPort = 9600,
        HttpPort = 8081,
        MaxClients = 12,
        Password = "secret",
        Track = "ks_monza",
        TrackLayout = string.Empty,
        CarsCsv = "car_a,car_b",
        PickupModeEnabled = true,
        LoopMode = false,
    };

    [Fact]
    public void ToServerCfgIni_Contains_ProfileFields()
    {
        var ini = _mapper.ToServerCfgIni(Sample());
        Assert.Contains("NAME=Wave Test", ini);
        Assert.Contains("TRACK=ks_monza", ini);
        Assert.Contains("CARS=car_a,car_b", ini);
        Assert.Contains("MAX_CLIENTS=12", ini);
        Assert.Contains("PASSWORD=secret", ini);
        Assert.Contains("LOOP_MODE=0", ini);
    }

    [Fact]
    public void ToServerCfgIni_Preserves_UnknownKeys_FromExisting()
    {
        var existing = "[SERVER]\nNAME=Old\nFUTURE_KEY=keep\n\n[WEATHER_0]\nGRAPHICS=3_clear\n";
        var ini = _mapper.ToServerCfgIni(Sample(), existing);
        Assert.Contains("FUTURE_KEY=keep", ini);
        Assert.Contains("[WEATHER_0]", ini);
        Assert.Contains("GRAPHICS=3_clear", ini);
        Assert.Contains("NAME=Wave Test", ini);
    }

    [Fact]
    public void ToEntryListIni_Generates_OneSection_PerCar()
    {
        var ini = _mapper.ToEntryListIni(Sample());
        Assert.Contains("[CAR_0]", ini);
        Assert.Contains("MODEL=car_a", ini);
        Assert.Contains("[CAR_1]", ini);
        Assert.Contains("MODEL=car_b", ini);
    }

    [Fact]
    public void ToEntryListIni_Preserves_Skin_When_Model_Unchanged()
    {
        var existing = "[CAR_0]\nMODEL=car_a\nSKIN=fluo\nDRIVERNAME=Alex\n";
        var ini = _mapper.ToEntryListIni(Sample(), existing);
        Assert.Contains("SKIN=fluo", ini);
        Assert.Contains("DRIVERNAME=Alex", ini);
    }

    [Fact]
    public void ToEntryListIni_Resets_Skin_When_Model_Changed()
    {
        var existing = "[CAR_0]\nMODEL=other_car\nSKIN=fluo\n";
        var ini = _mapper.ToEntryListIni(Sample(), existing);
        Assert.Contains("MODEL=car_a", ini);
        Assert.DoesNotContain("SKIN=fluo", ini);
    }
}
