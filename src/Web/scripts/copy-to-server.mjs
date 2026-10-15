// Copies the Vite build output into the ASP.NET Core wwwroot so the
// backend serves the UI from a single self-contained executable.
import { cpSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const dist = resolve(root, '..', 'dist');
const wwwroot = resolve(root, '..', '..', 'Server', 'wwwroot');

rmSync(wwwroot, { recursive: true, force: true });
mkdirSync(wwwroot, { recursive: true });
cpSync(dist, wwwroot, { recursive: true });
console.log(`Copied ${dist} -> ${wwwroot}`);
