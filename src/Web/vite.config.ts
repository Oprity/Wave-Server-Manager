import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Dev proxy targets the ASP.NET Core backend (see src/Server launchSettings: :5080).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
      '/hubs': {
        target: 'http://localhost:5080',
        ws: true,
      },
    },
  },
  test: {
    environment: 'node',
  },
});
