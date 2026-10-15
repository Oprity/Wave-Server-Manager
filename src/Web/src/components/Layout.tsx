// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { Link } from 'react-router-dom';
import { isDesktopHost } from '../utils/desktop';
import TitleBar from './TitleBar';

export default function Layout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100">
      {isDesktopHost() && <TitleBar />}
      <header className="border-b border-zinc-800">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-5">
            <Link to="/" className="text-lg font-bold tracking-tight">
              Wave Server Manager
            </Link>
            <nav className="flex gap-4 text-sm text-zinc-400">
              <Link to="/" className="hover:text-zinc-100">
                Profiles
              </Link>
              <Link to="/setup" className="hover:text-zinc-100">
                Setup
              </Link>
            </nav>
          </div>
          <span className="text-xs text-zinc-500">local-first · localhost only</span>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-4 py-6">{children}</main>
    </div>
  );
}
