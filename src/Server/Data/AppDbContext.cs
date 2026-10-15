// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using WaveServerManager.Models;
using Microsoft.EntityFrameworkCore;

namespace WaveServerManager.Data;

/// <summary>
/// EF Core database context. Intentionally provider-agnostic: no SQLite/SQL Server
/// specifics live here — provider selection happens in <see cref="DatabaseServiceExtensions"/>.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ServerProfile> Profiles => Set<ServerProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ServerProfile>(b =>
        {
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.Name);
            b.Property(p => p.Name).IsRequired().HasMaxLength(100);
            b.Property(p => p.ServerName).IsRequired().HasMaxLength(100);
            b.Property(p => p.Track).IsRequired().HasMaxLength(100);
            b.Property(p => p.CarsCsv).HasMaxLength(4000);
        });
    }
}
