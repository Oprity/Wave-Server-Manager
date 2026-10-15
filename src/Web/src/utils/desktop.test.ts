// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { describe, expect, it, afterEach } from 'vitest';
import { isDesktopHost, sendWindowCommand } from './desktop';

afterEach(() => {
  delete (globalThis as Record<string, unknown>).window;
});

describe('isDesktopHost', () => {
  it('is false with no window (node/test env)', () => {
    expect(isDesktopHost()).toBe(false);
  });

  it('is false when external.sendMessage is missing (plain browser)', () => {
    (globalThis as Record<string, unknown>).window = { external: {} };
    expect(isDesktopHost()).toBe(false);
  });

  it('is true inside the Photino shell', () => {
    (globalThis as Record<string, unknown>).window = { external: { sendMessage: () => undefined } };
    expect(isDesktopHost()).toBe(true);
  });
});

describe('sendWindowCommand', () => {
  it('sends wm:-prefixed messages in the shell and no-ops elsewhere', () => {
    const seen: string[] = [];
    (globalThis as Record<string, unknown>).window = { external: { sendMessage: (m: string) => seen.push(m) } };
    sendWindowCommand('close');
    expect(seen).toEqual(['wm:close']);

    delete (globalThis as Record<string, unknown>).window;
    expect(() => sendWindowCommand('minimize')).not.toThrow();
  });
});
