// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { api } from '../api/client';

interface LogEvent {
  profileId: string;
  line: string;
  at: string;
}

export default function LogPanel({ profileId }: { profileId: string }) {
  const [lines, setLines] = useState<string[]>([]);
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let cancelled = false;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/logs')
      .withAutomaticReconnect()
      .build();

    (async () => {
      try {
        const recent = await api.getRecentLogs(profileId).catch(() => [] as string[]);
        if (!cancelled) setLines(recent);
        await conn.start();
        await conn.invoke('JoinProfile', profileId);
        if (!cancelled) {
          setConnected(true);
          setError(null);
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      }
    })();

    conn.on('LogReceived', (ev: LogEvent) => {
      setLines((prev) => [...prev.slice(-499), ev.line]);
    });

    return () => {
      cancelled = true;
      conn
        .invoke('LeaveProfile', profileId)
        .catch(() => undefined)
        .finally(() => conn.stop().catch(() => undefined));
    };
  }, [profileId]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [lines]);

  return (
    <div className="rounded border border-zinc-700 bg-black">
      <div className="flex items-center justify-between border-b border-zinc-800 px-3 py-2">
        <span className="text-sm font-semibold text-zinc-200">Live log</span>
        <span className={`text-xs ${connected ? 'text-green-400' : 'text-zinc-500'}`}>
          {connected ? '● connected' : error ? `○ ${error}` : '○ connecting…'}
        </span>
      </div>
      <div className="h-64 overflow-y-auto px-3 py-2 font-mono text-xs leading-relaxed text-zinc-300">
        {lines.length === 0 && <div className="text-zinc-600">No output yet. Start the server to stream logs.</div>}
        {lines.map((l, i) => (
          <div key={i} className="whitespace-pre-wrap break-all">
            {l}
          </div>
        ))}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
