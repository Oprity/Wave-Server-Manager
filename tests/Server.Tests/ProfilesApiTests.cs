// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using AcServerManager.Dtos;
using AcServerManager.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AcServerManager.Tests;

public sealed class ProfilesApiTests : IClassFixture<ProfilesApiFactory>
{
    private readonly ProfilesApiFactory _factory;

    public ProfilesApiTests(ProfilesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Crud_Duplicate_And_Validation_Work()
    {
        using var client = _factory.CreateClient();

        // Create
        var create = new CreateProfileDto
        {
            Name = "Test Profile",
            ServerName = "Wave",
            TcpPort = 9600,
            UdpPort = 9600,
            HttpPort = 8081,
            MaxClients = 10,
            Track = "ks_monza",
            CarsCsv = "car_a,car_b",
        };
        var post = await client.PostAsJsonAsync("/api/profiles", create);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<ServerProfile>();
        Assert.NotNull(created);

        // List
        var list = await client.GetFromJsonAsync<List<ServerProfile>>("/api/profiles");
        Assert.Contains(list!, p => p.Id == created!.Id);

        // Get
        var single = await client.GetFromJsonAsync<ServerProfile>($"/api/profiles/{created!.Id}");
        Assert.Equal("Wave", single!.ServerName);

        // Update
        create.ServerName = "Wave v2";
        var put = await client.PutAsJsonAsync($"/api/profiles/{created.Id}", (UpdateProfileDto)new UpdateProfileDto
        {
            Name = create.Name,
            ServerName = "Wave v2",
            TcpPort = create.TcpPort,
            UdpPort = create.UdpPort,
            HttpPort = create.HttpPort,
            MaxClients = create.MaxClients,
            Track = create.Track,
            CarsCsv = create.CarsCsv,
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Duplicate
        var dup = await client.PostAsync($"/api/profiles/{created.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, dup.StatusCode);

        // INI endpoints
        var cfg = await client.GetStringAsync($"/api/profiles/{created.Id}/server-cfg");
        Assert.Contains("NAME=Wave v2", cfg);
        var entry = await client.GetStringAsync($"/api/profiles/{created.Id}/entry-list");
        Assert.Contains("[CAR_0]", entry);

        // Validation: bad track + HTTP port collision
        var badTrackDto = new CreateProfileDto
        {
            Name = "x",
            ServerName = create.ServerName,
            TcpPort = create.TcpPort,
            UdpPort = create.UdpPort,
            HttpPort = create.HttpPort,
            MaxClients = create.MaxClients,
            Track = "../evil",
            CarsCsv = create.CarsCsv,
        };
        var badTrack = await client.PostAsJsonAsync("/api/profiles", badTrackDto);
        Assert.Equal(HttpStatusCode.BadRequest, badTrack.StatusCode);
        var badPortsDto = new CreateProfileDto
        {
            Name = "y",
            ServerName = create.ServerName,
            TcpPort = create.TcpPort,
            UdpPort = create.UdpPort,
            HttpPort = 9600,
            MaxClients = create.MaxClients,
            Track = "ks_monza",
            CarsCsv = create.CarsCsv,
        };
        var badPorts = await client.PostAsJsonAsync("/api/profiles", badPortsDto);
        Assert.Equal(HttpStatusCode.BadRequest, badPorts.StatusCode);

        // Delete
        var del = await client.DeleteAsync($"/api/profiles/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }
}

/// <summary>API factory using an isolated in-memory database per run.</summary>
public sealed class ProfilesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Select the InMemory provider via configuration: no container surgery,
        // so EF10 never sees two providers composed on one options builder.
        builder.UseSetting("Database:Provider", "InMemory");
        builder.UseSetting("Database:ConnectionString", _dbName);
        builder.UseSetting("AcManager:AssettoServerExe", "");
    }
}
