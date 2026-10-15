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
