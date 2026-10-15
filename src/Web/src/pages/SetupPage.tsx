// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { ContentStatus } from '../types';

const inputCls =
  'w-full rounded border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm focus:border-emerald-500 focus:outline-none';

export default function SetupPage({ onSkip }: { onSkip?: () => void }) {
  const [status, setStatus] = useState<ContentStatus | null>(null);
  const [path, setPath] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function refresh() {
    setError(null);
    try {
      const s = await api.getContent();
      setStatus(s);
      setPath(s.manualPath ?? '');
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }

  useEffect(() => {
    refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const s = await api.setContentPath(path);
      setStatus(s);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  }

  async function clear() {
    setBusy(true);
    try {
      const s = await api.clearContentPath();
      setStatus(s);
      setPath('');
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  }

  if (!status) {
    return <p className="text-zinc-400">{error ?? 'Checking for Assetto Corsa…'}</p>;
  }

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <h1 className="text-xl font-bold">Game content setup</h1>

      {status.found ? (
        <div className="rounded border border-green-800 bg-green-950 px-4 py-3 text-sm text-green-200">
          Assetto Corsa found at <code className="font-mono">{status.effectivePath}</code>
          {status.manualPath ? ' (set manually)' : ' (detected from Steam)'}
        </div>
      ) : (
        <div className="space-y-3 rounded border border-amber-700 bg-amber-950 px-4 py-3 text-sm text-amber-200">
          <p className="font-semibold">Assetto Corsa was not found on this machine.</p>
          <p>The server needs your Assetto Corsa game files (cars, tracks). Pick one:</p>
          <div className="flex flex-wrap gap-2">
            <a
              href={status.steamInstallUrl}
              className="rounded bg-emerald-600 px-4 py-2 font-semibold text-white hover:bg-emerald-500"
            >
              Install via Steam
            </a>
            <a
              href={status.steamStoreUrl}
              target="_blank"
              rel="noreferrer"
              className="rounded bg-zinc-700 px-4 py-2 hover:bg-zinc-600"
            >
              Open store page
            </a>
            {onSkip && (
              <button onClick={onSkip} className="rounded bg-zinc-800 px-4 py-2 text-zinc-300 hover:bg-zinc-700">
                Continue without it
              </button>
            )}
          </div>
          <p className="text-xs text-amber-300/80">
            “Install via Steam” opens the Steam client install dialog (needs the game in your library).
          </p>
        </div>
      )}

      {error && (
        <div className="rounded border border-red-700 bg-red-950 px-4 py-3 text-sm text-red-200">{error}</div>
      )}

      <div className="rounded border border-zinc-800 bg-zinc-900 p-4">
        <h2 className="mb-2 text-sm font-semibold text-zinc-200">Set the folder manually</h2>
        <form onSubmit={save} className="flex flex-col gap-2 sm:flex-row">
          <input
            className={inputCls}
            value={path}
            onChange={(e) => setPath(e.target.value)}
            placeholder="C:\Program Files (x86)\Steam\steamapps\common\assettocorsa"
          />
          <button
            type="submit"
            disabled={busy || path.trim().length === 0}
            className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold hover:bg-emerald-500 disabled:opacity-50"
          >
            Save
          </button>
          {status.manualPath && (
            <button
              type="button"
              onClick={clear}
              disabled={busy}
              className="rounded bg-zinc-700 px-4 py-2 text-sm hover:bg-zinc-600 disabled:opacity-50"
            >
              Back to auto-detect
            </button>
          )}
        </form>
        <p className="mt-2 text-xs text-zinc-500">
          Paste the full path to your <code className="font-mono">assettocorsa</code> folder. It is checked
          on the server before saving.
        </p>
      </div>

      <div className="rounded border border-zinc-800 bg-zinc-900 p-4 text-sm">
        <div className="mb-1 flex items-center justify-between">
          <h2 className="font-semibold text-zinc-200">Detected on this machine</h2>
          <button onClick={refresh} className="rounded bg-zinc-700 px-3 py-1 text-xs hover:bg-zinc-600">
            Rescan
          </button>
        </div>
        {status.steamInstalls.length === 0 ? (
          <p className="text-zinc-400">No Steam installations found.</p>
        ) : (
          <ul className="list-inside list-disc text-zinc-300">
            {status.steamInstalls.map((s) => (
              <li key={s} className="font-mono text-xs">
                {s}
              </li>
            ))}
          </ul>
        )}
        {status.autoDetectedPath && (
          <p className="mt-2 text-zinc-400">
            Auto-detected game: <code className="font-mono text-xs">{status.autoDetectedPath}</code>
          </p>
        )}
      </div>
    </div>
  );
}
