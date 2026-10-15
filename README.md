# AC Server Manager

A Windows-first, **local-first, open-source** Assetto Corsa dedicated server manager.
Create, configure, run and monitor Assetto Corsa servers from a web UI on localhost —
the backend serves the built frontend, so the final product is **one self-contained executable**.

> **License: AGPL-3.0.** See [LICENSE](./LICENSE). Every source file carries a short
> `SPDX-License-Identifier: AGPL-3.0-or-later` header notice.

## Screenshots

> Milestone 1 placeholder — screenshots of the profile list, profile edit form and live
> log panel will go here once the UI is styled (`docs/screenshots/*.png`).

## Upstream credits

- **[AssettoServer](https://github.com/assetto-server/assetto) (AGPL-3.0 fork of the AC server ecosystem)**
  — this manager shells out to AssettoServer **0.54** (or stock `acServer.exe`); it does
  **not** bundle, copy or redistribute any server binaries or Kunos/game content.
- Original game + server protocol by **Kunos Simulazioni**.
- Built with ASP.NET Core, EF Core, SignalR, React, Vite and Tailwind (see NOTICE-worthy
  licenses in each package registry).

**Content policy:** the app *detects or downloads* the server binary on the user's own
machine (point `AcManager:AssettoServerExe` at it, or install AssettoServer 0.54 per its
docs). Never commit binaries, `content/`, `cars/`, `tracks/` or DLC files to this repo.

## Architecture

```
┌──────────────┐   HTTP/SignalR (localhost:5080)   ┌─────────────────────────┐
│ React + Vite │ ◄───────────────────────────────► │ ASP.NET Core Web API    │
│ Tailwind UI  │   /api/*  +  /hubs/logs (ws)      │  ProfilesController     │
└──────────────┘                                   │  ServerProcessManager ──┼──► acServer/AssettoServer child process
                                                   │  ProfileIniMapper ──────┼──► server_cfg.ini / entry_list.ini
                                                   │  EF Core + SQLite ──────┼──► ac-server-manager.db
                                                   └─────────────────────────┘
```

- **Backend** (`src/Server`, .NET 8 LTS): profile CRUD, EF Core models + migrations,
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

- [.NET 8 SDK](https://dotnet.microsoft.com/download) (latest LTS)
- [Node.js 20+](https://nodejs.org/) + npm
- AssettoServer **0.54** (or stock AC server) installed **by you** — download from
  [AssettoServer releases](https://github.com/assetto-server/assetto/releases),
  never from this repo.

### Run (dev)

```powershell
# 1. Backend (http://localhost:5080, Swagger in Development)
dotnet run --project src/Server

# 2. Frontend dev server with API/WS proxy (http://localhost:5173) — second terminal
cd src/Web
npm install
npm run dev
```

### Run (single executable, like production)

```powershell
cd src/Web
npm install
npm run build:server   # builds React and copies dist/ -> ../Server/wwwroot/
cd ../..
dotnet publish src/Server -c Release -o publish
./publish/AcServerManager   # UI at http://localhost:5080
```

### Configure

`src/Server/appsettings.json`:

```json
{
  "Database": { "Provider": "Sqlite", "ConnectionString": "Data Source=ac-server-manager.db" },
  "AcManager": {
    "DataDir": "",
    "AssettoServerExe": "C:\\Tools\\AssettoServer\\AssettoServer.exe"
  }
}
```

- Empty `DataDir` → `%AppData%\AcServerManager` (Windows) with `profiles/` + `logs/`
  created automatically on startup (feature 1). Check `GET /api/setup/paths`.
- Each profile gets `%AppData%\AcServerManager\profiles\<id>\server_cfg.ini` +
  `entry_list.ini`, generated from the form on server start (unknown keys/comments preserved).

### Docker (dev)

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

CI (`.github/workflows/ci.yml`) builds both projects and runs all tests on
Ubuntu + Windows.

## Roadmap

- **M1 (done):** repo scaffold, profile CRUD + SQLite + migrations, round-trip INI,
  process start/stop + SignalR logs, list/edit/log UI, README, CI, all green.
- **M2 (proposed):** content scanning (cars/skins/tracks dropdowns), INI import/preview
  diff, results viewer, Discord webhook, auto-restart polish, Windows installer/publish profile.
