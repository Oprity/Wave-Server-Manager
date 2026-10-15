// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;

namespace WaveServerManager.Data;

/// <summary>
/// Single place where the database provider is chosen.
/// Everything else in the app talks to <see cref="AppDbContext"/> only,
/// so adding SQL Server later means touching this file (plus a connection string).
/// </summary>
public static class DatabaseServiceExtensions
{
    public const string DefaultSqliteFileName = "wave-server-manager.db";

    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<string>("Database:Provider") ?? "Sqlite";
        var connectionString = config.GetValue<string>("Database:ConnectionString")
            ?? $"Data Source={DefaultSqliteFileName}";

        switch (provider.Trim().ToLowerInvariant())
        {
            case "sqlite":
                services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
                break;
            case "inmemory":
                // Tests and throwaway dev runs only: connectionString doubles as the store name.
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(connectionString));
                break;
            case "sqlserver":
                // Extension point for Milestone 3+: add Microsoft.EntityFrameworkCore.SqlServer
                // and call o.UseSqlServer(connectionString) here. No other file needs changes.
                throw new NotSupportedException(
                    "SQL Server provider is not wired up yet. Add the SqlServer EF Core package " +
                    "and implement the 'sqlserver' branch in DatabaseServiceExtensions.");
            default:
                throw new InvalidOperationException(
                    $"Unknown Database:Provider '{provider}'. Expected 'Sqlite' (or 'InMemory' for tests).");
        }

        return services;
    }
}
