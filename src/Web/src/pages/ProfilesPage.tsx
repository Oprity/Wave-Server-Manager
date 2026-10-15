// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { ServerProfile, ServerStatus, SetupInfo } from '../types';

export default function ProfilesPage() {
  const [profiles, setProfiles] = useState<ServerProfile[]>([]);
  const [statuses, setStatuses] = useState<Record<string, ServerStatus>>({});
  const [setup, setSetup] = useState<SetupInfo | null>(null);
  const [contentFound, setContentFound] = useState<boolean | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    const list = await api.listProfiles();
    setProfiles(list);
    const st: Record<string, ServerStatus> = {};
    await Promise.all(
      list.map(async (p) => {
        try {
          st[p.id] = await api.getStatus(p.id);
        } catch {
          /* ignore per-profile status errors */
        }
      }),
    );
    setStatuses(st);
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const [_, s, c] = await Promise.all([
          refresh(),
          api.setup().catch(() => null),
          api.getContent().catch(() => null),
        ]);
        setSetup(s);
        setContentFound(c ? c.found : null);
      } catch (e) {
        setError(e instanceof Error ? e.message : String(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [refresh]);

  async function act(id: string, fn: (id: string) => Promise<ServerStatus>, label: string) {
    setBusy(`${label}:${id}`);
    setError(null);
    try {
      const st = await fn(id);
      setStatuses((prev) => ({ ...prev, [id]: st }));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  }

  async function remove(id: string) {
    if (!window.confirm('Delete this profile? The server will be stopped first.')) return;
    setBusy(`delete:${id}`);
    try {
      await api.deleteProfile(id);
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  }

  async function duplicate(id: string) {
    setBusy(`duplicate:${id}`);
    try {
      await api.duplicateProfile(id);
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  }

  if (loading) return <p className="text-zinc-400">Loading…</p>;

  return (
    <div className="space-y-4">
      {contentFound === false && (
        <div className="rounded border border-amber-700 bg-amber-950 px-4 py-3 text-sm text-amber-200">
          Assetto Corsa game files were not found — servers need them to run.{' '}
          <Link to="/setup" className="font-semibold underline">
            Open setup
          </Link>{' '}
          to auto-detect Steam, install, or set the folder manually.
        </div>
      )}
      {setup && !setup.assettoServerExists && (
        <div className="rounded border border-amber-700 bg-amber-950 px-4 py-3 text-sm text-amber-200">
          No AssettoServer binary detected. Install AssettoServer (v0.0.55 pre-release) from{' '}
          <a
            className="underline"
            href="https://github.com/compujuckel/AssettoServer/releases"
            target="_blank"
            rel="noreferrer"
          >
            compujuckel/AssettoServer releases
          </a>{' '}
          and set <code className="font-mono">AcManager:AssettoServerExe</code> in appsettings.json (see
          README). Data folder: <code className="font-mono">{setup.dataDir}</code>
        </div>
      )}
      {error && (
        <div className="rounded border border-red-700 bg-red-950 px-4 py-3 text-sm text-red-200">{error}</div>
      )}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold">Server profiles</h1>
        <Link to="/profiles/new" className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold hover:bg-emerald-500">
          New profile
        </Link>
      </div>
      {profiles.length === 0 ? (
        <p className="text-zinc-400">No profiles yet. Create one to get started.</p>
      ) : (
        <ul className="space-y-3">
          {profiles.map((p) => {
            const st = statuses[p.id];
            return (
              <li key={p.id} className="rounded border border-zinc-800 bg-zinc-900 p-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <div className="font-semibold">
                      {p.name}{' '}
                      <span className={`ml-2 text-xs ${st?.isRunning ? 'text-green-400' : 'text-zinc-500'}`}>
                        {st ? (st.isRunning ? `● running${st.pid ? ` (pid ${st.pid})` : ''}` : '○ stopped') : '…'}
                      </span>
                    </div>
                    <div className="text-sm text-zinc-400">
                      {p.serverName} · {p.track} · {p.carsCsv} · TCP {p.tcpPort} / UDP {p.udpPort} / HTTP {p.httpPort} ·{' '}
                      {p.maxClients} clients
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2 text-sm">
                    {!st?.isRunning ? (
                      <button
                        className="rounded bg-green-700 px-3 py-1 hover:bg-green-600 disabled:opacity-50"
                        disabled={busy !== null}
                        onClick={() => act(p.id, api.start, 'start')}
                      >
                        Start
                      </button>
                    ) : (
                      <>
                        <button
                          className="rounded bg-red-700 px-3 py-1 hover:bg-red-600 disabled:opacity-50"
                          disabled={busy !== null}
                          onClick={() => act(p.id, api.stop, 'stop')}
                        >
                          Stop
                        </button>
                        <button
                          className="rounded bg-amber-700 px-3 py-1 hover:bg-amber-600 disabled:opacity-50"
                          disabled={busy !== null}
                          onClick={() => act(p.id, api.restart, 'restart')}
                        >
                          Restart
                        </button>
                      </>
                    )}
                    <Link to={`/profiles/${p.id}`} className="rounded bg-zinc-700 px-3 py-1 hover:bg-zinc-600">
                      Edit / logs
                    </Link>
                    <button
                      className="rounded bg-zinc-700 px-3 py-1 hover:bg-zinc-600 disabled:opacity-50"
                      disabled={busy !== null}
                      onClick={() => duplicate(p.id)}
                    >
                      Duplicate
                    </button>
                    <button
                      className="rounded bg-zinc-800 px-3 py-1 text-red-300 hover:bg-zinc-700 disabled:opacity-50"
                      disabled={busy !== null}
                      onClick={() => remove(p.id)}
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
