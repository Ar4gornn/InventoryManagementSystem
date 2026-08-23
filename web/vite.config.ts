import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
// A project page is served from /<repo>/, not the domain root, so every asset URL
// has to be prefixed. Supplied by the Pages workflow; empty everywhere else, which
// keeps `npm run dev` and the Docker image serving from /.
const base = process.env.VITE_BASE ?? "/";

export default defineConfig({
  base,
  plugins: [react()],
  server: {
    // Fixed so the API CORS allow-list can name it, and so it does not collide
    // with other dev servers in this workspace.
    port: 5174,
    strictPort: true,
  },
});
