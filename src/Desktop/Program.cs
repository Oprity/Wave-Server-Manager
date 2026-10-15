// AC Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 AC Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AcServerManager.Hosting;
using Photino.NET;

namespace AcServerManager.Desktop;

/// <summary>
/// Desktop entry point: starts the ASP.NET Core backend in-process on a free
/// localhost port, shows it in a native Photino.NET (WebView2) window, and owns
/// a system-tray icon with Open / Quit. Closing the window shuts everything down.
/// Everything runs on the STA main thread (required by WinForms and WebView2);
/// async backend calls are blocked on (GetResult) so continuations never migrate
/// the window off this thread.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(a => string.Equals(a, "--smoke", StringComparison.OrdinalIgnoreCase)))
        {
            return SmokeSelfTest.Run();
        }

        var backendUrl = GetArgValue(args, "--backend-url");
        return DesktopRunner.Run(backendUrl);
    }

    internal static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i + 1 < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}

/// <summary>Orchestrates backend lifetime + native window. No WinForms types here
/// except the OK-only error dialog, so --smoke stays headless-capable.</summary>
internal static class DesktopRunner
{
    public static int Run(string? backendUrlOverride)
    {
        var url = backendUrlOverride ?? $"http://127.0.0.1:{FindFreePort()}";

        var app = AppHost.Create(["--urls", url]);
        try
        {
            app.StartAsync().GetAwaiter().GetResult();

            if (!WaitForHealth(url, TimeSpan.FromSeconds(30)))
            {
                ShowError($"The local server did not start in time.\n\nURL: {url}");
                app.StopAsync().GetAwaiter().GetResult();
                return 1;
            }

            Application.EnableVisualStyles();

            PhotinoWindow? window = null;
            using var tray = new TrayMenu(AppTitleConst, OnOpen, OnQuit);
            window = new PhotinoWindow()
                .SetTitle(AppTitleConst)
                .SetUseOsDefaultSize(false)
                .SetSize(new System.Drawing.Size(1280, 800))
                .Center()
                .SetResizable(true)
                .SetDevToolsEnabled(false)
                .SetContextMenuEnabled(false)
                .Load(url);

            // Blocks on the native message loop (which also pumps tray messages).
            // Closing the window returns here, then we shut the backend down.
            window.WaitForClose();
            return 0;

            void OnOpen()
            {
                // Photino v4 has no Show/BringToFront; a momentary TopMost
                // reliably foregrounds the window.
                var w = window;
                if (w is null)
                {
                    return;
                }
                w.Invoke(() =>
                {
                    w.SetTopMost(true);
                    w.SetTopMost(false);
                });
            }

            void OnQuit() => window?.Close();
        }
        finally
        {
            app.StopAsync().GetAwaiter().GetResult();
            app.DisposeAsync().GetAwaiter().GetResult();
        }
    }

    private const string AppTitleConst = "AC Server Manager";

    internal static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    internal static bool WaitForHealth(string baseUrl, TimeSpan timeout)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var res = http.GetAsync(baseUrl.TrimEnd('/') + "/healthz").GetAwaiter().GetResult();
                if (res.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Backend not up yet; keep polling.
            }
            Thread.Sleep(250);
        }
        return false;
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, AppTitleConst, MessageBoxButtons.OK, MessageBoxIcon.Error);
}

/// <summary>
/// Headless self-test for CI and troubleshooting: starts the backend on a free
/// port, waits for /healthz, shuts down cleanly. No window, no tray.
/// </summary>
internal static class SmokeSelfTest
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    public static int Run()
    {
        if (OperatingSystem.IsWindows())
        {
            AllocConsole();
        }

        var url = $"http://127.0.0.1:{DesktopRunner.FindFreePort()}";
        Console.WriteLine($"[smoke] starting backend at {url}");
        try
        {
            using var app = AppHost.Create(["--urls", url]);
            app.StartAsync().GetAwaiter().GetResult();
            try
            {
                if (!DesktopRunner.WaitForHealth(url, TimeSpan.FromSeconds(30)))
                {
                    Console.WriteLine("[smoke] FAIL: backend never became healthy");
                    return 1;
                }
                Console.WriteLine("[smoke] OK: backend healthy, shutting down");
                return 0;
            }
            finally
            {
                app.StopAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[smoke] FAIL: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
