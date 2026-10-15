// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using AcServerManager.Data;
using AcServerManager.Hubs;
using AcServerManager.Services;
using Microsoft.EntityFrameworkCore;

namespace AcServerManager.Hosting;

/// <summary>
/// Builds the ASP.NET Core backend application. Used both by the standalone
/// <c>Program</c> entry point and by the Photino.NET desktop host, which starts
/// the backend in-process on a free localhost port.
/// </summary>
public static class AppHost
{
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSignalR();

        builder.Services.AddAppDatabase(builder.Configuration);
        builder.Services.AddSingleton<AcPathsService>();
        builder.Services.AddSingleton<ProfileIniMapper>();
        builder.Services.AddSingleton<IServerProcessManager, ServerProcessManager>();

        builder.Services.AddCors(o => o.AddPolicy("dev", p => p
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        var app = builder.Build();

        // Ensure local-first folders exist (Milestone 1, feature 1).
        app.Services.GetRequiredService<AcPathsService>().EnsureCreated();

        // Apply EF Core migrations on startup (SQLite file is created if missing).
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // Integration tests swap in the InMemory provider, which has no migrations.
                if (db.Database.IsRelational())
                {
                    db.Database.Migrate();
                }
                else
                {
                    db.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed.");
                throw;
            }
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseCors("dev");
        }

        app.MapGet("/healthz", () => Results.Ok(new { ok = true, at = DateTimeOffset.UtcNow }));

        app.MapControllers();
        app.MapHub<LogsHub>("/hubs/logs");

        // Serve the built React frontend (src/Web/dist copied to wwwroot) as a single executable.
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapFallbackToFile("index.html");

        return app;
    }
}
