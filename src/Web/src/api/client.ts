// Wave Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import type { ServerProfile, ServerStatus, SetupInfo } from '../types';

async function req<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
  });
  if (!res.ok) {
    const body = await res.text().catch(() => '');
    let detail = body;
    try {
      const json = JSON.parse(body) as { error?: string; title?: string };
      detail = json.error ?? json.title ?? body;
    } catch {
      /* keep raw body */
    }
    throw new Error(`${res.status} ${res.statusText}${detail ? `: ${detail}` : ''}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  listProfiles: () => req<ServerProfile[]>('/api/profiles'),
  getProfile: (id: string) => req<ServerProfile>(`/api/profiles/${id}`),
  createProfile: (dto: Partial<ServerProfile>) =>
    req<ServerProfile>('/api/profiles', { method: 'POST', body: JSON.stringify(dto) }),
  updateProfile: (id: string, dto: Partial<ServerProfile>) =>
    req<ServerProfile>(`/api/profiles/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
  deleteProfile: (id: string) => req<void>(`/api/profiles/${id}`, { method: 'DELETE' }),
  duplicateProfile: (id: string) =>
    req<ServerProfile>(`/api/profiles/${id}/duplicate`, { method: 'POST' }),
  getStatus: (id: string) => req<ServerStatus>(`/api/profiles/${id}/status`),
  getRecentLogs: (id: string) => req<string[]>(`/api/profiles/${id}/logs`),
  start: (id: string) => req<ServerStatus>(`/api/profiles/${id}/start`, { method: 'POST' }),
  stop: (id: string) => req<ServerStatus>(`/api/profiles/${id}/stop`, { method: 'POST' }),
  restart: (id: string) => req<ServerStatus>(`/api/profiles/${id}/restart`, { method: 'POST' }),
  setup: () => req<SetupInfo>('/api/setup/paths'),
};
