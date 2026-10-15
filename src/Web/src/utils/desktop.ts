// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

/** Commands the chromeless desktop window understands (see Desktop Program.cs). */
export type WindowCommand = 'minimize' | 'maximize' | 'close' | 'drag';

declare global {
  // NOTE: DOM lib already declares window.external (legacy IE shape), so we
  // read it structurally instead of redeclaring it.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface WindowEventMap {}
}

interface PhotinoExternal {
  sendMessage?: (message: string) => void;
}

function photinoExternal(): PhotinoExternal | undefined {
  try {
    return (window as unknown as { external?: PhotinoExternal }).external;
  } catch {
    return undefined;
  }
}

/** True when running inside the Photino desktop shell (not a plain browser). */
export function isDesktopHost(): boolean {
  try {
    return typeof window !== 'undefined' && typeof photinoExternal()?.sendMessage === 'function';
  } catch {
    return false;
  }
}

/** Ask the native shell to minimize/maximize/close/begin-drag. No-op in browsers. */
export function sendWindowCommand(command: WindowCommand): void {
  const ext = typeof window === 'undefined' ? undefined : photinoExternal();
  ext?.sendMessage?.(`wm:${command}`);
}
