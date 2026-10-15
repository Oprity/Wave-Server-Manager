// AC Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

export interface ServerProfile {
  id: string;
  name: string;
  serverName: string;
  tcpPort: number;
  udpPort: number;
  httpPort: number;
  maxClients: number;
  password?: string | null;
  adminPassword?: string | null;
  track: string;
  trackLayout?: string | null;
  carsCsv: string;
  pickupModeEnabled: boolean;
  loopMode: boolean;
  autoRestartOnCrash: boolean;
  executablePath?: string | null;
  executableArgs?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ServerStatus {
  profileId: string;
  isRunning: boolean;
  pid?: number | null;
  startedAt?: string | null;
  executable?: string | null;
}

export interface SetupInfo {
  dataDir: string;
  profilesDir: string;
  logsDir: string;
  assettoServerExe: string;
  assettoServerExists: boolean;
}
