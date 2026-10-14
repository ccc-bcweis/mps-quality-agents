// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import path from "node:path";
import { fileURLToPath } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const root = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // contracts/msw/handlers.ts lives outside this package and imports "msw".
      msw: path.resolve(root, "node_modules/msw"),
    },
  },
  server: {
    port: 5173,
    fs: { allow: [path.resolve(root, "..")] },
    proxy: {
      "/api": "http://localhost:8080",
      "/health": "http://localhost:8080",
      "/hubs": { target: "http://localhost:8080", ws: true },
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
