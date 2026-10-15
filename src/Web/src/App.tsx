// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { useEffect, useState } from 'react';
import { Route, Routes } from 'react-router-dom';
import { api } from './api/client';
import Layout from './components/Layout';
import ProfilesPage from './pages/ProfilesPage';
import ProfileEditPage from './pages/ProfileEditPage';
import SetupPage from './pages/SetupPage';

type Gate = 'loading' | 'setup' | 'app';

export default function App() {
  const [gate, setGate] = useState<Gate>('loading');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const content = await api.getContent();
        if (!cancelled) setGate(content.found ? 'app' : 'setup');
      } catch {
        // Never brick the UI if the check fails; the banner covers it.
        if (!cancelled) setGate('app');
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (gate === 'loading') {
    return (
      <Layout>
        <p className="text-zinc-400">Starting…</p>
      </Layout>
    );
  }

  if (gate === 'setup') {
    return (
      <Layout>
        <SetupPage onSkip={() => setGate('app')} />
      </Layout>
    );
  }

  return (
    <Layout>
      <Routes>
        <Route path="/" element={<ProfilesPage />} />
        <Route path="/profiles/new" element={<ProfileEditPage />} />
        <Route path="/profiles/:id" element={<ProfileEditPage />} />
        <Route path="/setup" element={<SetupPage />} />
      </Routes>
    </Layout>
  );
}
