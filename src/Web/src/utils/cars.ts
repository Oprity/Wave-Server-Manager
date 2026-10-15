// AC Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

/** Split a cars CSV into folder names, mirroring the backend NormalizeCars. */
export function parseCarsCsv(csv: string): string[] {
  return csv
    .split(',')
    .map((c) => c.trim())
    .filter((c) => c.length > 0);
}

export function joinCarsCsv(cars: string[]): string {
  return cars.map((c) => c.trim()).filter((c) => c.length > 0).join(',');
}

const CONTENT_NAME = /^[A-Za-z0-9_-]+$/;

export function isValidContentName(v: string): boolean {
  return CONTENT_NAME.test(v);
}

export function validateProfileFields(input: {
  track: string;
  carsCsv: string;
  httpPort: number;
  tcpPort: number;
  udpPort: number;
}): string | null {
  if (!isValidContentName(input.track.trim())) {
    return "Track must be a content folder name (letters, digits, '_' or '-').";
  }
  for (const car of parseCarsCsv(input.carsCsv)) {
    if (!isValidContentName(car)) {
      return `Invalid car folder name: '${car}'.`;
    }
  }
  if (input.httpPort === input.tcpPort || input.httpPort === input.udpPort) {
    return 'HTTP port must differ from TCP/UDP ports.';
  }
  return null;
}
