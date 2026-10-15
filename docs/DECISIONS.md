# Decisions (append-only log)

## M1 — 2026-10-15

1. **.NET 8 (LTS), not .NET 10.** Only the .NET 9 SDK is installed on this machine and
   offline SDK installs are out of scope; `net8.0` is the newest LTS that builds here.
   Upgrade path: bump `TargetFramework` + EF packages to 10.x when the .NET 10 SDK is
   available. Recorded so a reviewer knows the "latest LTS" deviation is environmental.
2. **Cars stored as CSV string**, not a join table. Keeps the model trivially portable
   across SQLite/SQL Server and maps 1:1 to both `server_cfg.ini` (`CARS=`) and
   `entry_list.ini` (`[CAR_N]`). Revisit if per-car metadata (skin, ballast) needs querying.
3. **INI engine preserves formatting only for untouched lines.** Modified keys are
   re-emitted as `KEY=VALUE` + original trailing comment. Full whitespace fidelity for
   edited lines was rejected as complexity without user value.
4. **Inline comments require preceding whitespace** (`VALUE ; note`). Protects passwords
   like `p;ss`. Documented limitation: a value containing ` ;` will be split — acceptable
   for AC configs, flagged in code comments.
5. **Process manager passes `ExecutableArgs` verbatim.** Stock acServer needs none; the
   field exists for AssettoServer flags and makes the manager testable with `ping`/`sleep`.
6. **Migrations run at startup** (`Migrate()`), falling back to `EnsureCreated()` for the
   InMemory provider used by API tests. Single-exe story needs zero manual DB steps.
7. **`wwwroot/` is generated, not committed.** Built via `npm run build:server`; CI
   reproduces it before `dotnet publish`. Keeps the repo free of build artifacts.
8. **No secrets/paths in repo.** `appsettings.json` ships with empty `AssettoServerExe`;
   per-machine values stay in `appsettings.Development.json` (uncommitted overrides) or env.
9. **CI matrix ubuntu + windows.** Windows-first product, but INI/API logic must be
   OS-agnostic; the process test picks `ping.exe` vs `/bin/sleep` by OS.
10. **Track/cars are free-text inputs in M1** with folder-name validation; real dropdowns
    wait for M2 content scanning.

## Desktop shell — 2026-10-15

11. **Photino.NET 4.0.16 for the native window.** Targets `net8.0` (matches the backend);
    API verified by reflection + a compile probe (`new PhotinoWindow()`, fluent setters,
    `Load(url)`, `WaitForClose()`). WebView2 fallback was not needed. WebView2 Runtime
    is a prerequisite but ships with Windows 11.
12. **Backend extracted to `AppHost.Create`, behavior unchanged.** `Program.cs` is now
    three lines; the desktop host starts the same app in-process. `WebApplicationFactory<Program>`
    tests keep working.
13. **Free loopback port per launch** (`TcpListener(Loopback, 0)`), passed via `--urls`;
    `--backend-url` override points the window at an existing backend for dev.
    Readiness is polled on `/healthz` (30 s); failure shows a WinForms error dialog.
14. **Tray = WinForms NotifyIcon** (Photino v4 has no tray/Show/Hide API):
    Open foregrounds via momentary `SetTopMost`, Quit calls `window.Close()`,
    double-click = Open. Closing the window always stops the backend (`StopAsync`) —
    no minimize-to-tray, by request ("shut everything down cleanly on close").
15. **STA main thread, blocking waits.** `Main` is synchronous; async backend calls use
    `GetAwaiter().GetResult()` so the Photino/WebView2 window never migrates off the
    STA thread. Tray types live in `TrayMenu` so `--smoke` never loads WinForms.
16. **No custom icons yet** (`SystemIcons.Application` for the tray, OS default window
    icon). Backend console logs are unavailable in WinExe mode — future work: file sink
    + "View logs" tray item.
17. **CI builds Desktop on both OSes** (`EnableWindowsTargeting`) but publishes +
    `--smoke` on Windows only (needs the WindowsDesktop runtime + WebView2 adjacency).
    `wwwroot/` is mirrored into Desktop publish output via a linked Content item.
