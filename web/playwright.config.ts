// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.pw.ts",
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: "list",
  outputDir: "../TestResults/playwright",
  use: {
    baseURL: "http://127.0.0.1:5174",
    channel: "msedge",
    trace: "off",
    screenshot: "only-on-failure",
  },
  webServer: {
    command: "npx vite --host 127.0.0.1 --port 5174 --strictPort",
    url: "http://127.0.0.1:5174",
    reuseExistingServer: false,
    timeout: 120_000,
  },
  projects: [
    { name: "1280", use: { viewport: { width: 1280, height: 900 } } },
    { name: "1600", use: { viewport: { width: 1600, height: 900 } } },
    { name: "1920", use: { viewport: { width: 1920, height: 900 } } },
  ],
});
