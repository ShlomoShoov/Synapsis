/**
 * How the client is built, served while developing, and tested: Vite builds and serves it, Vitest runs its tests.
 * One file for both, so the tests resolve imports exactly as the app does.
 */
import { fileURLToPath, URL } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],

  // The local assets folder: everything in assets/ is served as it is, at the site root.
  // assets/media/home/horizon.jpg is the address /media/home/horizon.jpg.
  publicDir: 'assets',

  // `@/` means `src/`, so imports read the same from any folder: `import { areas } from '@/site/areas'`.
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },

  // The development server (npm run dev). The deployed client is served by nginx on port 1897 instead.
  // /es and /api reach the core's Elasticsearch and API, so Observe needs no cross-origin access while developing
  // (src/observe/config.ts).
  server: { port: 5173, strictPort: true, proxy: coreProxy() },

  build: {
    // The built code goes to dist/app/ with content hashes in its names, so the server can cache it for a year.
    // The name differs from assets/ on purpose: assets/ holds what people add, app/ holds what the build makes.
    assetsDir: 'app',
    target: 'es2023',
  },

  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
});

/**
 * The development server's forwarding to the core (deploy/core/compose.yml): /es to Elasticsearch on port 9200,
 * /api to the C# API on port 8080, each without its prefix.
 */
function coreProxy() {
  return {
    '/es': {
      target: 'http://localhost:9200',
      rewrite: (path: string) => path.replace(/^\/es/, ''),
    },
    '/api': {
      target: 'http://localhost:8080',
      rewrite: (path: string) => path.replace(/^\/api/, ''),
    },
  };
}
