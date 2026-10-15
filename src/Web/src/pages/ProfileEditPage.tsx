// AC Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import LogPanel from '../components/LogPanel';
import { validateProfileFields } from '../utils/cars';

const inputCls =
  'w-full rounded border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm focus:border-emerald-500 focus:outline-none';

export default function ProfileEditPage() {
  const { id } = useParams();
  const isNew = !id || id === 'new';
  const navigate = useNavigate();

  const [form, setForm] = useState({
    name: 'My server',
    serverName: 'My AC Server',
    tcpPort: 9600,
    udpPort: 9600,
    httpPort: 8081,
    maxClients: 18,
    password: '',
    adminPassword: '',
    track: 'ks_nurburgring',
    trackLayout: '',
    carsCsv: 'ks_porsche_911_gt3_rs',
    pickupModeEnabled: true,
    loopMode: true,
    autoRestartOnCrash: false,
    executablePath: '',
    executableArgs: '',
  });
  const [loading, setLoading] = useState(!isNew);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isNew) return;
    (async () => {
      try {
        const p = await api.getProfile(id!);
        setForm({
          name: p.name,
          serverName: p.serverName,
          tcpPort: p.tcpPort,
          udpPort: p.udpPort,
          httpPort: p.httpPort,
          maxClients: p.maxClients,
          password: '',
          adminPassword: '',
          track: p.track,
          trackLayout: p.trackLayout ?? '',
          carsCsv: p.carsCsv,
          pickupModeEnabled: p.pickupModeEnabled,
          loopMode: p.loopMode,
          autoRestartOnCrash: p.autoRestartOnCrash,
          executablePath: p.executablePath ?? '',
          executableArgs: p.executableArgs ?? '',
        });
      } catch (e) {
        setError(e instanceof Error ? e.message : String(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [id, isNew]);

  function set<K extends keyof typeof form>(key: K, value: (typeof form)[K]) {
    setForm((f) => ({ ...f, [key]: value }));
  }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const err = validateProfileFields({
      track: form.track,
      carsCsv: form.carsCsv,
      httpPort: Number(form.httpPort),
      tcpPort: Number(form.tcpPort),
      udpPort: Number(form.udpPort),
    });
    if (err) {
      setError(err);
      return;
    }
    setSaving(true);
    try {
      const dto = {
        ...form,
        tcpPort: Number(form.tcpPort),
        udpPort: Number(form.udpPort),
        httpPort: Number(form.httpPort),
        maxClients: Number(form.maxClients),
        password: form.password || undefined,
        adminPassword: form.adminPassword || undefined,
        trackLayout: form.trackLayout || undefined,
        executablePath: form.executablePath || undefined,
        executableArgs: form.executableArgs || undefined,
      };
      const saved = isNew ? await api.createProfile(dto) : await api.updateProfile(id!, dto);
      navigate(`/profiles/${saved.id}`);
    } catch (e2) {
      setError(e2 instanceof Error ? e2.message : String(e2));
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <p className="text-zinc-400">Loading…</p>;

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-bold">{isNew ? 'New profile' : 'Edit profile'}</h1>
      {error && (
        <div className="rounded border border-red-700 bg-red-950 px-4 py-3 text-sm text-red-200">{error}</div>
      )}
      <form onSubmit={save} className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <label className="block">
          <span className="text-sm text-zinc-300">Profile name</span>
          <input className={inputCls} value={form.name} maxLength={100} onChange={(e) => set('name', e.target.value)} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">Server name (in-game)</span>
          <input className={inputCls} value={form.serverName} maxLength={100} onChange={(e) => set('serverName', e.target.value)} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">TCP port</span>
          <input className={inputCls} type="number" min={1} max={65535} value={form.tcpPort} onChange={(e) => set('tcpPort', Number(e.target.value))} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">UDP port</span>
          <input className={inputCls} type="number" min={1} max={65535} value={form.udpPort} onChange={(e) => set('udpPort', Number(e.target.value))} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">HTTP port</span>
          <input className={inputCls} type="number" min={1} max={65535} value={form.httpPort} onChange={(e) => set('httpPort', Number(e.target.value))} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">Max clients</span>
          <input className={inputCls} type="number" min={0} max={48} value={form.maxClients} onChange={(e) => set('maxClients', Number(e.target.value))} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">Password (optional)</span>
          <input className={inputCls} type="password" value={form.password} placeholder={isNew ? '' : '(unchanged if blank — editing clears it)'} onChange={(e) => set('password', e.target.value)} />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">Track folder</span>
          <input className={inputCls} value={form.track} maxLength={100} onChange={(e) => set('track', e.target.value)} required />
        </label>
        <label className="block md:col-span-2">
          <span className="text-sm text-zinc-300">Cars (comma-separated folder names; dropdowns arrive in M2 content scanning)</span>
          <input className={inputCls} value={form.carsCsv} onChange={(e) => set('carsCsv', e.target.value)} required />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">Track layout (optional)</span>
          <input className={inputCls} value={form.trackLayout} onChange={(e) => set('trackLayout', e.target.value)} />
        </label>
        <label className="block">
          <span className="text-sm text-zinc-300">Executable override (optional)</span>
          <input className={inputCls} value={form.executablePath} placeholder="Defaults to AcManager:AssettoServerExe" onChange={(e) => set('executablePath', e.target.value)} />
        </label>
        <div className="flex flex-wrap gap-6 md:col-span-2">
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.pickupModeEnabled} onChange={(e) => set('pickupModeEnabled', e.target.checked)} />
            Pickup mode
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.loopMode} onChange={(e) => set('loopMode', e.target.checked)} />
            Loop mode
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.autoRestartOnCrash} onChange={(e) => set('autoRestartOnCrash', e.target.checked)} />
            Auto-restart on crash
          </label>
        </div>
        <div className="md:col-span-2">
          <button type="submit" disabled={saving} className="rounded bg-emerald-600 px-5 py-2 text-sm font-semibold hover:bg-emerald-500 disabled:opacity-50">
            {saving ? 'Saving…' : 'Save profile'}
          </button>
        </div>
      </form>
      {!isNew && id && <LogPanel profileId={id} />}
    </div>
  );
}
