// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using WaveServerManager.Hubs;
using WaveServerManager.Models;
using Microsoft.AspNetCore.SignalR;

namespace WaveServerManager.Services;

public sealed record ServerStatusDto(Guid ProfileId, bool IsRunning, int? Pid, DateTimeOffset? StartedAt, string? Executable);

public interface IServerProcessManager
{
    ServerStatusDto GetStatus(Guid profileId);
    IReadOnlyList<string> GetRecentLogs(Guid profileId);
    Task<ServerStatusDto> StartAsync(ServerProfile profile, CancellationToken ct = default);
    Task StopAsync(Guid profileId, CancellationToken ct = default);
}

/// <summary>
/// Starts/stops the acServer (or compatible AssettoServer) child process and
/// streams stdout/stderr lines to SignalR group <c>profile-{id}</c>.
/// Supports optional crash auto-restart via <see cref="ServerProfile.AutoRestartOnCrash"/>.
/// </summary>
public sealed class ServerProcessManager : IServerProcessManager, IDisposable
{
    private const int RecentLogCapacity = 500;

    private readonly IHubContext<LogsHub> _hub;
    private readonly AcPathsService _paths;
    private readonly IConfiguration _config;
    private readonly ILogger<ServerProcessManager> _log;
    private readonly ConcurrentDictionary<Guid, RunningServer> _running = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<string>> _recentLogs = new();
    private bool _disposed;

    public ServerProcessManager(
        IHubContext<LogsHub> hub,
        AcPathsService paths,
        IConfiguration config,
        ILogger<ServerProcessManager> log)
    {
        _hub = hub;
        _paths = paths;
        _config = config;
        _log = log;
    }

    public ServerStatusDto GetStatus(Guid profileId)
    {
        if (_running.TryGetValue(profileId, out var rs) && !rs.Process.HasExited)
        {
            return new ServerStatusDto(profileId, true, TryPid(rs.Process), rs.StartedAt, rs.Executable);
        }
        return new ServerStatusDto(profileId, false, null, null, null);
    }

    public IReadOnlyList<string> GetRecentLogs(Guid profileId) =>
        _recentLogs.TryGetValue(profileId, out var q) ? q.ToArray() : Array.Empty<string>();

    public async Task<ServerStatusDto> StartAsync(ServerProfile profile, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(profile);

        if (_running.TryGetValue(profile.Id, out var existing) && !existing.Process.HasExited)
        {
            return GetStatus(profile.Id);
        }

        var exe = ResolveExecutable(profile);
        var workDir = _paths.ProfileDir(profile.Id);

        // Persist current INI files next to the process so acServer picks them up.
        await WriteIniFilesAsync(profile, workDir, ct).ConfigureAwait(false);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = profile.ExecutableArgs ?? string.Empty,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var rs = new RunningServer(process, DateTimeOffset.UtcNow, exe, profile.AutoRestartOnCrash, profile);

        process.OutputDataReceived += (_, e) => OnLine(profile.Id, e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(profile.Id, e.Data);
        process.Exited += (_, _) => OnExited(profile.Id);

        if (!_running.TryAdd(profile.Id, rs))
        {
            process.Dispose();
            return GetStatus(profile.Id);
        }

        try
        {
            if (!process.Start())
            {
                _running.TryRemove(profile.Id, out _);
                throw new InvalidOperationException($"Failed to start '{exe}'.");
            }
        }
        catch (Exception ex)
        {
            _running.TryRemove(profile.Id, out _);
            process.Dispose();
            _log.LogError(ex, "Failed to start server process for profile {ProfileId}", profile.Id);
            throw new InvalidOperationException($"Could not start server executable '{exe}': {ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await EmitAsync(profile.Id, $"--- started pid={process.Id} exe={exe} ---").ConfigureAwait(false);
        _log.LogInformation("Started profile {ProfileId} pid={Pid}", profile.Id, process.Id);
        return GetStatus(profile.Id);
    }

    public async Task StopAsync(Guid profileId, CancellationToken ct = default)
    {
        if (!_running.TryRemove(profileId, out var rs))
        {
            return;
        }

        rs.ManualStop = true;
        try
        {
            var proc = rs.Process;
            if (!proc.HasExited)
            {
                await EmitAsync(profileId, "--- stopping ---").ConfigureAwait(false);
                proc.Kill(entireProcessTree: true);
#if NET8_0_OR_GREATER
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
#else
                proc.WaitForExit(5000);
#endif
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Error stopping profile {ProfileId}", profileId);
        }
        finally
        {
            rs.Process.Dispose();
        }

        await EmitAsync(profileId, "--- stopped ---").ConfigureAwait(false);
        _log.LogInformation("Stopped server process for profile {ProfileId}", profileId);
    }

    private void OnLine(Guid profileId, string? line)
    {
        if (line is null)
        {
            return;
        }
        AppendRecent(profileId, line);
        // Fire-and-forget: never block the process output pump.
        _ = _hub.Clients.Group(LogsHub.GroupName(profileId.ToString()))
            .SendAsync("LogReceived", new { profileId, line, at = DateTimeOffset.UtcNow });
    }

    private Task EmitAsync(Guid profileId, string line)
    {
        AppendRecent(profileId, line);
        return _hub.Clients.Group(LogsHub.GroupName(profileId.ToString()))
            .SendAsync("LogReceived", new { profileId, line, at = DateTimeOffset.UtcNow });
    }

    private void AppendRecent(Guid profileId, string line)
    {
        var q = _recentLogs.GetOrAdd(profileId, _ => new ConcurrentQueue<string>());
        q.Enqueue(line);
        while (q.Count > RecentLogCapacity && q.TryDequeue(out _))
        {
        }
    }

    private void OnExited(Guid profileId)
    {
        if (!_running.TryGetValue(profileId, out var rs))
        {
            return;
        }

        int code;
        try { code = rs.Process.ExitCode; } catch { code = -1; }

        _ = EmitAsync(profileId, $"--- process exited (code {code}) ---");
        _running.TryRemove(profileId, out _);
        try { rs.Process.Dispose(); } catch { }

        if (rs.AutoRestart && !rs.ManualStop)
        {
            _log.LogInformation("Auto-restarting profile {ProfileId} in 5s (exit {Code})", profileId, code);
            _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(async _ =>
            {
                if (_disposed || _running.ContainsKey(profileId))
                {
                    return;
                }
                try
                {
                    await EmitAsync(profileId, "--- auto-restarting after crash ---");
                    await StartAsync(rs.ProfileSnapshot).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Auto-restart failed for profile {ProfileId}", profileId);
                    await EmitAsync(profileId, $"--- auto-restart failed: {ex.Message} ---");
                }
            });
        }
    }

    private string ResolveExecutable(ServerProfile profile)
    {
        var candidate = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath.Trim()
            : _config.GetValue<string>("AcManager:AssettoServerExe") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new InvalidOperationException(
                "No server executable configured. Set AcManager:AssettoServerExe in appsettings.json " +
                "or per-profile ExecutablePath. The app never bundles binaries — install AssettoServer " +
                "from compujuckel/AssettoServer releases yourself (see README).");
        }

        // Bare command names (no directory) resolve via PATH, e.g. "ping" in tests.
        if (candidate.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0 &&
            !Path.IsPathRooted(candidate))
        {
            var found = FindOnPath(candidate);
            if (found is not null)
            {
                return found;
            }
            throw new FileNotFoundException($"Server executable not found on PATH: '{candidate}'.", candidate);
        }

        var full = Path.GetFullPath(candidate);
        if (!System.IO.File.Exists(full))
        {
            throw new FileNotFoundException($"Server executable not found: '{full}'.", full);
        }

        return full;
    }

    private static string? FindOnPath(string fileName)
    {
        // Direct hit (e.g. "C:\...\ping.exe" passed without separators is impossible, but be lenient).
        if (System.IO.File.Exists(fileName))
        {
            return Path.GetFullPath(fileName);
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : new[] { string.Empty };

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedDir = dir.Trim().Trim('"');
            if (trimmedDir.Length == 0)
            {
                continue;
            }
            foreach (var ext in extensions)
            {
                var name = fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ext;
                string full;
                try
                {
                    full = Path.Combine(trimmedDir, name);
                }
                catch
                {
                    continue;
                }
                if (System.IO.File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    private async Task WriteIniFilesAsync(ServerProfile profile, string workDir, CancellationToken ct)
    {
        // Resolve mapper without DI cycles: construct directly (pure logic, no deps).
        var mapper = new ProfileIniMapper();
        var cfgPath = Path.Combine(workDir, "server_cfg.ini");
        var entryPath = Path.Combine(workDir, "entry_list.ini");

        string? existingCfg = System.IO.File.Exists(cfgPath)
            ? await System.IO.File.ReadAllTextAsync(cfgPath, ct).ConfigureAwait(false) : null;
        string? existingEntry = System.IO.File.Exists(entryPath)
            ? await System.IO.File.ReadAllTextAsync(entryPath, ct).ConfigureAwait(false) : null;

        await System.IO.File.WriteAllTextAsync(cfgPath, mapper.ToServerCfgIni(profile, existingCfg), ct)
            .ConfigureAwait(false);
        await System.IO.File.WriteAllTextAsync(entryPath, mapper.ToEntryListIni(profile, existingEntry), ct)
            .ConfigureAwait(false);
    }

    private static int? TryPid(Process p)
    {
        try { return p.HasExited ? null : p.Id; } catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        foreach (var (id, rs) in _running)
        {
            try
            {
                rs.ManualStop = true;
                if (!rs.Process.HasExited)
                {
                    rs.Process.Kill(entireProcessTree: true);
                }
                rs.Process.Dispose();
            }
            catch { }
        }
        _running.Clear();
    }

    private sealed class RunningServer
    {
        public RunningServer(Process process, DateTimeOffset startedAt, string executable, bool autoRestart, ServerProfile snapshot)
        {
            Process = process;
            StartedAt = startedAt;
            Executable = executable;
            AutoRestart = autoRestart;
            ProfileSnapshot = snapshot;
        }

        public Process Process { get; }
        public DateTimeOffset StartedAt { get; }
        public string Executable { get; }
        public bool AutoRestart { get; }
        public ServerProfile ProfileSnapshot { get; }
        public bool ManualStop { get; set; }
    }
}
