// Wave Server Manager — https://github.com/ (see README)
// Copyright (C) 2026 Wave Server Manager contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WaveServerManager.Hosting;
using WaveServerManager.Logging;
using Photino.NET;

namespace WaveServerManager.Desktop;

/// <summary>
/// Desktop entry point: starts the ASP.NET Core backend in-process on a free
/// localhost port, shows it in a native Photino.NET (WebView2) window, and owns
/// a system-tray icon with Open / View logs / Quit. Closing the window shuts
/// everything down. Everything runs on the STA main thread (required by WinForms
/// and WebView2); async backend calls are blocked on (GetResult) so continuations
/// never migrate the window off this thread.
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

        // Single instance: a second launch focuses the running window and exits,
        // so repeated double-clicks can never pile up zombie backends.
        using var mutex = new Mutex(true, @"Global\WaveServerManager.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            FocusExistingWindow();
            return 0;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogFiles.AppendLine($"FATAL unhandled exception: {e.ExceptionObject}");
            if (OperatingSystem.IsWindows())
            {
                MessageBox.Show(
                    $"Wave Server Manager crashed:\n\n{e.ExceptionObject}",
                    "Wave Server Manager",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogFiles.AppendLine($"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            var backendUrl = GetArgValue(args, "--backend-url");
            var code = DesktopRunner.Run(backendUrl);
            // Belt and suspenders: the WebView2/STA teardown can keep CLR threads
            // alive after the window closes; terminate deterministically.
            Environment.Exit(code);
            return code;
        }
        catch (Exception ex)
        {
            LogFiles.AppendLine($"FATAL startup failure: {ex}");
            return 1;
        }
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

    private const int SwRestore = 9;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    /// <summary>Brings the already-running window forward (best effort).</summary>
    private static void FocusExistingWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            var hwnd = FindWindow(null, "Wave Server Manager");
            if (hwnd == 0)
            {
                return;
            }
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SwRestore);
            }
            SetForegroundWindow(hwnd);
        }
        catch
        {
            // Focusing is a courtesy; never fail startup over it.
        }
    }
}

/// <summary>Orchestrates backend lifetime + native window.</summary>
internal static class DesktopRunner
{
    private const string AppTitleConst = "Wave Server Manager";

    public static int Run(string? backendUrlOverride)
    {
        LogFiles.PruneOldFiles();
        LogFiles.AppendLine("Desktop starting.");

        var url = backendUrlOverride ?? $"http://127.0.0.1:{FindFreePort()}";
        LogFiles.AppendLine($"Starting backend at {url}.");

        // Pin the content root to the exe folder: appsettings.json and wwwroot
        // live next to the executable, independent of the working directory.
        var app = AppHost.Create(["--urls", url], AppContext.BaseDirectory);
        try
        {
            app.StartAsync().GetAwaiter().GetResult();

            if (!WaitForHealth(url, TimeSpan.FromSeconds(30)))
            {
                LogFiles.AppendLine($"Backend at {url} never became healthy; exiting.");
                ShowError($"The local server did not start in time.\n\nURL: {url}\n\nSee the log folder:\n{LogFiles.DefaultDirectory}");
                app.StopAsync().GetAwaiter().GetResult();
                return 1;
            }

            LogFiles.AppendLine($"Backend healthy at {url}; opening window.");
            Application.EnableVisualStyles();

            PhotinoWindow? window = null;
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            using var tray = new TrayMenu(AppTitleConst, iconPath, OnOpen, OnViewLogs, OnQuit);
            window = new PhotinoWindow()
                .SetTitle(AppTitleConst)
                .SetUseOsDefaultSize(false)
                .SetChromeless(true)
                .RegisterWebMessageReceivedHandler(HandleWebMessage)
                .SetSize(new System.Drawing.Size(1280, 800))
                .Center()
                // Chromeless windows must not use OS-default size/location.
                .SetUseOsDefaultLocation(false)
                .SetResizable(true)
                .SetDevToolsEnabled(false)
                .SetContextMenuEnabled(false);
            if (File.Exists(iconPath))
            {
                window.SetIconFile(iconPath);
            }
            // Uri overload: the string overload would be mistaken for a file path.
            window.Load(new Uri(url));

            // Blocks on the native message loop (which also pumps tray messages).
            // Closing the window returns here, then we shut the backend down.
            window.WaitForClose();
            LogFiles.AppendLine("Window closed; stopping backend.");
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

            void OnViewLogs() => OpenLogsFolder();

            void OnQuit() => window?.Close();

            void HandleWebMessage(object? sender, string message)
            {
                var w = window;
                if (w is null)
                {
                    return;
                }
                // Marshal onto the Photino UI thread; messages arrive from the
                // WebView2 bridge on an arbitrary thread.
                w.Invoke(() =>
                {
                    switch (message)
                    {
                        case "wm:minimize":
                            w.SetMinimized(true);
                            break;
                        case "wm:maximize":
                            w.SetMaximized(!w.Maximized);
                            break;
                        case "wm:close":
                            w.Close();
                            break;
                        case "wm:drag":
                            BeginWindowDrag(w.WindowHandle);
                            break;
                    }
                });
            }
        }
        finally
        {
            app.StopAsync().GetAwaiter().GetResult();
            app.DisposeAsync().GetAwaiter().GetResult();
            LogFiles.AppendLine("Backend stopped; exiting.");
        }
    }

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

    internal static void OpenLogsFolder()
    {
        var dir = LogFiles.DefaultDirectory;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    /// <summary>Standard borderless-drag: let Windows move the window as if its
    /// caption was grabbed. Called on the UI thread via Photino Invoke.</summary>
    private static void BeginWindowDrag(nint hwnd)
    {
        if (hwnd == 0 || !OperatingSystem.IsWindows())
        {
            return;
        }
        ReleaseCapture();
        SendMessage(hwnd, WmNcLButtonDown, HtCaption, 0);
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

        LogFiles.PruneOldFiles();
        var url = $"http://127.0.0.1:{DesktopRunner.FindFreePort()}";
        var line = $"[smoke] starting backend at {url}";
        Console.WriteLine(line);
        LogFiles.AppendLine(line);
        try
        {
            using var app = AppHost.Create(["--urls", url]);
            app.StartAsync().GetAwaiter().GetResult();
            try
            {
                if (!DesktopRunner.WaitForHealth(url, TimeSpan.FromSeconds(30)))
                {
                    Console.WriteLine("[smoke] FAIL: backend never became healthy");
                    LogFiles.AppendLine("[smoke] FAIL: backend never became healthy");
                    return 1;
                }
                Console.WriteLine("[smoke] OK: backend healthy, shutting down");
                LogFiles.AppendLine("[smoke] OK: backend healthy, shutting down");
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
            LogFiles.AppendLine($"[smoke] FAIL: {ex}");
            return 1;
        }
    }
}
