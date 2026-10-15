# Wave Server Manager

A Windows-first, **local-first, open-source** Assetto Corsa dedicated server manager.
Create, configure, run and monitor Assetto Corsa servers from a desktop app on localhost —
no browser needed: the ASP.NET Core backend and the React UI run inside a native
Photino.NET (WebView2) window, with a system-tray icon for Open / Quit.

> **License: AGPL-3.0.** See [LICENSE](./LICENSE). Every source file carries a short
> `SPDX-License-Identifier: AGPL-3.0-or-later` header notice.

## Screenshots

> Placeholder — screenshots of the desktop window (profile list, profile edit form and
> live log panel) will go here (`docs/screenshots/*.png`).

## Upstream credits

- **[AssettoServer](https://github.com/compujuckel/AssettoServer) by compujuckel
  (AGPL-3.0, the original custom AC server)** — this manager shells out to
  AssettoServer (currently the **v0.0.55** pre-release line, or stock `acServer.exe`); it does
  **not** bundle, copy or redistribute any server binaries or Kunos/game content.
- Original game + server protocol by **Kunos Simulazioni**.
- [Photino.NET](https://github.com/tryphotino/photino.NET) (Apache-2.0) for the native window.
- Built with ASP.NET Core, EF Core, SignalR, React, Vite and Tailwind (see NOTICE-worthy
  licenses in each package registry).

**Content policy:** the app *detects or downloads* the server binary on the user's own
machine (point `AcManager:AssettoServerExe` at it, or install AssettoServer from the
[compujuckel/AssettoServer releases](https://github.com/compujuckel/AssettoServer/releases)
per its docs). Never commit binaries, `content/`, `cars/`, `tracks/` or DLC files to this repo.

## Architecture

```
┌────────────────────────────────────────────────────────┐
│ WaveServerManager.Desktop (native window, WebView2)      │
│  free 127.0.0.1 port → in-process backend ─┐           │
│  tray icon: Open / Quit                      │           │
└──────────────────────────────────────────────┼──────────┘
                                               ▼
┌─────────────────────────┐   HTTP/SignalR     ┌─────────────────────────┐
│ React + Vite            │ ◄── localhost ───► │ ASP.NET Core Web API    │
│ Tailwind UI (wwwroot/)  │  /api/* + /hubs    │  ProfilesController     │
└─────────────────────────┘                    │  ServerProcessManager ──┼──► acServer/AssettoServer child process
                                               │  ProfileIniMapper ──────┼──► server_cfg.ini / entry_list.ini
                                               │  EF Core + SQLite ──────┼──► wave-server-manager.db
                                               └─────────────────────────┘
```

- **Desktop** (`src/Desktop`, .NET 10 + Photino.NET 4): finds a free loopback port, starts
  the backend in-process (`AppHost.Create`), opens the window on it, owns the tray icon.
  Closing the window stops the backend and exits. `--smoke` runs a headless self-test.
- **Backend** (`src/Server`, .NET 10 LTS): profile CRUD, EF Core models + migrations,
  round-trip-safe INI reader/writer, process manager streaming stdout over SignalR,
  serves `wwwroot/` (the built React app) with `MapFallbackToFile("index.html")`.
- **Database:** SQLite via EF Core. All provider selection lives in
  `src/Server/Data/DatabaseServiceExtensions.cs` — no SQLite-specific code anywhere else,
  so SQL Server can be added later as an optional provider.
- **Frontend** (`src/Web`, TS + React + Vite + Tailwind): profile list, profile edit form
  (name, ports, password, max clients, track, cars), live log panel over SignalR.
- Details: [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) · Decisions: [docs/DECISIONS.md](./docs/DECISIONS.md)

## Setup

### Prerequisites

- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (preinstalled on Windows 11)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (latest LTS) — to build
- [Node.js 20+](https://nodejs.org/) + npm — to build the UI
- AssettoServer installed **by you** (or stock AC server) — grab the newest
  `assetto-server-win-x64.zip` from
  [compujuckel/AssettoServer releases](https://github.com/compujuckel/AssettoServer/releases),
  extract it, and point `AcManager:AssettoServerExe` at the server executable.
  Never from this repo.

### Run — desktop app (normal use)

```powershell
# Build the UI once, stage it for the backend
cd src/Web
npm install   # first time only
npm run build:server
cd ../..

# Publish the desktop app as a single self-contained exe
dotnet publish src/Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o desktop-publish

./desktop-publish/WaveServerManager.Desktop.exe
```

A native window opens with the manager UI, plus a tray icon (Open / View logs / Quit).
Closing the window stops the backend and quits everything.

Logs roll daily to `%LocalAppData%\WaveServerManager\logs\wave-YYYY-MM-DD.log`
(kept 7 days) — open the folder any time from the tray's **View logs** item.

Headless self-test (verifies backend boot + shutdown, no window):

```powershell
./desktop-publish/WaveServerManager.Desktop.exe --smoke
```

Useful flags: `--backend-url http://127.0.0.1:5080` (point the window at an
already-running backend instead of starting one).

### Run — backend + browser (development)

```powershell
# 1. Backend (http://localhost:5080, Swagger in Development)
dotnet run --project src/Server

# 2. Frontend dev server with API/WS proxy (http://localhost:5173) — second terminal
cd src/Web
npm install
npm run dev
```

### Configure

`src/Server/appsettings.json` (shipped next to the exe on publish):

```json
{
  "Database": { "Provider": "Sqlite", "ConnectionString": "Data Source=wave-server-manager.db" },
  "AcManager": {
    "DataDir": "",
    "AssettoServerExe": "C:\\Tools\\AssettoServer\\AssettoServer.exe"
  }
}
```

- Empty `DataDir` → `%AppData%\WaveServerManager` (Windows) with `profiles/` + `logs/`
  created automatically on startup (feature 1). Check `GET /api/setup/paths`.
- Each profile gets `%AppData%\WaveServerManager\profiles\<id>\server_cfg.ini` +
  `entry_list.ini`, generated from the form on server start (unknown keys/comments preserved).

### Docker (dev, backend + browser flow)

```powershell
docker compose up --build
```

API at `http://localhost:5080`, Vite dev at `http://localhost:5173`.

## API quick reference

| Method | Path | Notes |
|---|---|---|
| GET/POST | `/api/profiles` | list / create |
| GET/PUT/DELETE | `/api/profiles/{id}` | read / update / delete (stops server first) |
| POST | `/api/profiles/{id}/duplicate` | clone a profile |
| GET | `/api/profiles/{id}/server-cfg` | rendered `server_cfg.ini` |
| GET | `/api/profiles/{id}/entry-list` | rendered `entry_list.ini` |
| GET | `/api/profiles/{id}/status` | `{ isRunning, pid, … }` |
| GET | `/api/profiles/{id}/logs` | recent stdout lines |
| POST | `/api/profiles/{id}/start|stop|restart` | process control |
| GET | `/api/setup/paths` | data dirs + binary detection |
| WS | `/hubs/logs` | `JoinProfile(id)` → `LogReceived` events |

## Tests

```powershell
dotnet test                       # backend: xUnit (INI round-trip, mapper, API, process)
cd src/Web; npm run test          # frontend: Vitest (CSV/validation utils)
```

CI (`.github/workflows/ci.yml`) builds all projects and runs all tests on
Ubuntu + Windows, plus publishes the desktop single-exe and runs `--smoke` on Windows.

## Roadmap

- **M1 (done):** repo scaffold, profile CRUD + SQLite + migrations, round-trip INI,
  process start/stop + SignalR logs, list/edit/log UI, README, CI, all green.
- **Desktop (done):** Photino.NET native window, free-port in-process backend, tray
  Open/Quit, clean shutdown, `--smoke` self-test.
- **Next:** content scanning (cars/skins/tracks dropdowns), INI import/preview
  diff, results viewer, Discord webhook, AssettoServer release downloader, installer.
