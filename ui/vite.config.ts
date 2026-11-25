import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The UI is served by the .NET host (embedded) or by the dev host with --ui-dir.
export default defineConfig({
  plugins: [react()],
  base: './',
  build: { outDir: 'dist', emptyOutDir: true, assetsInlineLimit: 0, chunkSizeWarningLimit: 900 },
  server: { port: 5173 },
});
