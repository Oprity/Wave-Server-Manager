# Architecture (Milestone 1)

## Backend — `src/Server` (ASP.NET Core, .NET 8)

| File | Role |
|---|---|
| `Program.cs` | DI wiring, EF `Migrate()` on startup (or `EnsureCreated` for non-relational test provider), static-file serving of `wwwroot/`, `MapHub<LogsHub>`, `/healthz` |
| `Models/ServerProfile.cs` | Profile entity; `CarsCsv` stored as one string to stay provider-agnostic |
| `Data/AppDbContext.cs` | `DbSet<Profiles>` + fluent config; **no provider specifics** |
| `Data/DatabaseServiceExtensions.cs` | **Only file that names SQLite.** `sqlserver` branch throws `NotSupportedException` with instructions — the SQL-Server extension point |
| `Data/Migrations/*` | `InitialCreate` + `AddExecutableArgs` |
| `Dtos/ProfileDtos.cs` | Create/update DTOs + shared validation (`ProfileRules`) |
| `Controllers/ProfilesController.cs` | CRUD, duplicate, `server-cfg`/`entry-list` rendering, start/stop/restart/status/logs |
| `Controllers/SetupController.cs` | `GET /api/setup/paths` — folders + binary detection |
| `Ini/IniDocument.cs` | Round-trip-safe INI: preserves section order, unknown keys, `;`/`#` comments, blank lines; `SetValue` rewrites only touched keys (trailing comments kept) |
| `Services/ProfileIniMapper.cs` | Profile ⇄ `server_cfg.ini` / `entry_list.ini`; per-slot SKIN/DRIVERNAME preserved when MODEL unchanged |
| `Services/ServerProcessManager.cs` | `Process` start/stop, stdout/stderr → SignalR group `profile-{id}`, 500-line ring buffer, optional crash auto-restart (5 s delay), kills tree on stop/dispose |
| `Services/AcPathsService.cs` | `%AppData%\AcServerManager` (Win) / `~/.local/share/ac-server-manager` (*nix), creates `profiles/`, `logs/` |
| `Services/PathGuards.cs` | Path-traversal validation helpers |
| `Hubs/LogsHub.cs` | `JoinProfile`/`LeaveProfile` group management with Guid validation |

## Frontend — `src/Web` (React + Vite + Tailwind)

| File | Role |
|---|---|
| `src/pages/ProfilesPage.tsx` | List, status badges, start/stop/restart/duplicate/delete, binary-missing banner |
| `src/pages/ProfileEditPage.tsx` | Form (name, ports, password, max clients, track, cars CSV, flags, exe override) + `LogPanel` |
| `src/components/LogPanel.tsx` | SignalR client: recent-logs fetch + `JoinProfile` → live `LogReceived` stream |
| `src/api/client.ts` | Typed `fetch` wrapper with server `error` extraction |
| `src/utils/cars.ts` | CSV parse/join + content-name validation (mirrors backend rules) |
| `scripts/copy-to-server.mjs` | `npm run build:server` → copies `dist/` into `src/Server/wwwroot/` |

## Data flow (start server)

1. UI `POST /api/profiles/{id}/start`
2. `ServerProcessManager` resolves exe (per-profile override → `AcManager:AssettoServerExe` → PATH lookup), writes `server_cfg.ini`/`entry_list.ini` into the profile dir (merging over existing files to keep unknown keys), spawns the child with output redirection
3. Stdout lines → SignalR `LogReceived` to group `profile-{id}` + ring buffer (`GET …/logs` for late joiners)
4. Exit without manual stop + `AutoRestartOnCrash` → restart after 5 s
