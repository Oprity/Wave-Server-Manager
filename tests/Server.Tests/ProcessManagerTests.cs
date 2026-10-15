// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using AcServerManager.Hubs;
using AcServerManager.Models;
using AcServerManager.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AcServerManager.Tests;

public sealed class ProcessManagerTests
{
    private static (ServerProcessManager Manager, Mock<IHubContext<LogsHub>> Hub) CreateManager()
    {
        var hub = new Mock<IHubContext<LogsHub>>();
        var clients = new Mock<IHubClients>();
        var group = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var tmp = Path.Combine(Path.GetTempPath(), $"acsm-test-{Guid.NewGuid():N}");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AcManager:DataDir"] = tmp,
            })
            .Build();

        var paths = new AcPathsService(config);
        paths.EnsureCreated();
        var manager = new ServerProcessManager(hub.Object, paths, config, NullLogger<ServerProcessManager>.Instance);
        return (manager, hub);
    }

    [Fact]
    public async Task Start_MissingExecutable_Throws_FileNotFound()
    {
        var (manager, _) = CreateManager();
        using (manager)
        {
            var profile = new ServerProfile
            {
                ExecutablePath = Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.exe"),
            };
            await Assert.ThrowsAsync<FileNotFoundException>(() => manager.StartAsync(profile));
        }
    }

    [Fact]
    public async Task Start_Without_AnyExecutable_Configured_Throws_InvalidOperation()
    {
        var (manager, _) = CreateManager();
        using (manager)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(new ServerProfile()));
        }
    }

    [Fact]
    public async Task Start_LongRunningProcess_Then_Stop_Works()
    {
        var (manager, _) = CreateManager();
        using (manager)
        {
            // Long-running command with explicit args (keeps the test fast: we stop it).
            string exe, args;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe");
                args = "127.0.0.1 -n 30";
            }
            else if (File.Exists("/bin/sleep"))
            {
                exe = "/bin/sleep";
                args = "30";
            }
            else
            {
                return; // minimal container without sleep: skip rather than fail.
            }

            if (!File.Exists(exe))
            {
                return;
            }

            var profile = new ServerProfile { ExecutablePath = exe, ExecutableArgs = args };
            var status = await manager.StartAsync(profile);
            Assert.True(status.IsRunning);
            Assert.NotNull(status.Pid);

            await Task.Delay(1000);
            Assert.True(manager.GetStatus(profile.Id).IsRunning);

            await manager.StopAsync(profile.Id);
            Assert.False(manager.GetStatus(profile.Id).IsRunning);
        }
    }

    [Fact]
    public void Status_UnknownProfile_Is_NotRunning()
    {
        var (manager, _) = CreateManager();
        using (manager)
        {
            var status = manager.GetStatus(Guid.NewGuid());
            Assert.False(status.IsRunning);
            Assert.Null(status.Pid);
        }
    }
}
