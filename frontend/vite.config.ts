import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
// Built assets are embedded into PiNo.Labs.VariationsAuditor.Core.dll and served same-origin from the
// addon DLL by Optimizely CMS. base must match the static path the host serves the bundle from.
export default defineConfig({
  plugins: [react()],
  base: '/variations-auditor/',
  build: {
    // Emit into the Core project's PRIVATE intermediate folder, from where Core.csproj embeds the bundle
    // into Core.dll. The bundle is NEVER written to the Foundation host wwwroot - the NuGet addon is the
    // single source of truth for the UI.
    outDir: '../src/PiNo.Labs.VariationsAuditor.Core/obj/frontend-dist',
    emptyOutDir: true,
  },
  server: {
    // DEV ONLY - local Vite dev server proxy to the running Foundation CMS host.
    // Production does NOT use this: the bundle is served from wwwroot and calls same-origin /api/auditor.
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://localhost:5000',
        changeOrigin: true,
        secure: false,
      },
    },
  },
});