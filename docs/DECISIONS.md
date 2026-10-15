# Decisions (append-only log)

## M1 — 2026-10-15

1. **.NET 10 (LTS), supported through Nov 2028.** Retargeted from .NET 8 once the
   .NET 10 SDK was available (EF Core 10.0.x, `global.json` pins `10.0.400` with
   `latestFeature` roll-forward). Photino.NET 4.0.16 ships `net8.0`/`net9.0` assets
   only; its `net8.0` build rolls forward onto .NET 10 with no code changes —
   verified by the desktop launch check (window alive, backend serving).
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

11. **Photino.NET 4.0.16 for the native window.** Ships `net8.0`/`net9.0` assets; the
    `net8.0` build rolls forward onto .NET 10 with no code changes (verified, see #1).
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

## File logging — 2026-10-15

18. **Rolling daily logs, no external sink package.** `Logging/LogFiles` owns the path
    (`%LocalAppData%\WaveServerManager\logs\wave-YYYY-MM-DD.log`, pruned to 7 days by
    last-write time); `Logging/FileLoggerProvider` is a ~50-line `ILoggerProvider`
    (Information+) registered in `AppHost`, so backend lifetime + process start/stop
    lines land in the file even though WinExe has no console. The desktop host logs
    startup, backend readiness and shutdown to the same file, plus `AppDomain` /
    `UnobservedTask` handlers for crashes. Tray **View logs** opens the folder via
    `UseShellExecute`. Covered by `FileLoggingTests` (naming, append, prune, levels).

## Upstream AssettoServer — 2026-10-15

19. **Real upstream is [compujuckel/AssettoServer](https://github.com/compujuckel/AssettoServer)**
    (original, AGPL-3.0 — not the `assetto-server/assetto` fork previously credited).
    Release line is **v0.0.55 pre-releases** with per-OS assets
    (`assetto-server-win-x64.zip` for Windows). All docs, UI text and error messages
    now point there. Follow-up: in-app downloader that fetches + extracts the Windows
    asset into the user's data dir and fills in `AssettoServerExe` automatically.
    Verified live: v0.0.55-pre44 boots from manager-generated INI files
    ("Loaded 2 cars"); it then needs the user's AC game content, which only the
    user can provide. Note it logs `cfg\server_cfg.ini` — content-path wiring is
    M2 content-scanning work.

## Rename to Wave Server Manager — 2026-10-15

20. **Product is "Wave Server Manager"** (matches the GitHub repo name). Namespaces,
    assemblies, window/tray titles, README, log messages and data folders
    (`%AppData%\WaveServerManager`, `%LocalAppData%\WaveServerManager\logs`) all use
    the new name; final exe is `WaveServerManager.Desktop.exe`. Directory layout
    (`src/Server`, `src/Desktop`, `src/Web`, `tests/Server.Tests`) intentionally kept:
    folders are organizational, and stable paths keep CI/Docker/scripts untouched.
    Solution file is the new SDK-default `.slnx` format (created by .NET 10 CLI).

## Single-file controller discovery — 2026-10-15

21. **The desktop single exe needs `AddApplicationPart`.** With the Desktop host as
    entry assembly, single-file publish breaks automatic referenced-assembly
    controller scanning (`/healthz` worked, every `/api/*` fell through to the SPA).
    `AppHost` now registers the Server assembly explicitly; harmless standalone.

## Desktop content root — 2026-10-15

22. **Pin the backend content root to the exe folder.** Double-clicking `start.bat`
    launches with the repo as working directory, so the backend looked for
    `wwwroot/` and `appsettings.json` in the wrong place (UI 404, default config).
    `AppHost.Create` takes an optional content root; the desktop host passes
    `AppContext.BaseDirectory`, and the launcher also sets the working directory.
    Covered by `AppHostTests` (explicit root honored, no disk writes).

## App icon — 2026-10-15

23. **Custom emerald-on-dark wave icon** (`src/Desktop/app.ico`, generated with
    multi-size PNG entries). Embedded in the exe via `ApplicationIcon`; the window
    (`SetIconFile`) and tray load the file copy, which needs
    `ExcludeFromSingleFile=true` or it gets swallowed into the bundle (same trap
    as `wwwroot/` would be without the linked-content copy).

## Steam game detection — 2026-10-15

22. **Find every Steam install, or show the setup screen.** `SteamContentLocator`
    reads the registry + default paths, parses new- and legacy-format
    `libraryfolders.vdf`, and accepts a folder with `AssettoCorsa.exe`/`content/`
    (appid 244210). `ContentPathsService` layers a validated manual override
    (`content.json`) over auto-detection; stale overrides are ignored.
    `GET/POST/DELETE /api/setup/content` backs a first-run setup screen
    (install via `steam://install/244210`, store link, manual path, skip) plus a
    permanent Setup page and a profiles banner. A browser can't return a folder
    path, so manual entry is a validated text field. Covered by
    `ContentDetectionTests` against fake Steam roots.
