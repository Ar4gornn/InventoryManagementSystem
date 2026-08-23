import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Fixed so the API CORS allow-list can name it, and so it does not collide
    // with other dev servers in this workspace.
    port: 5174,
    strictPort: true,
  },
});
