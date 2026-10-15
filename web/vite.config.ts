// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig, type Plugin } from "vitest/config";
import { parseFleetSensors, type FleetSensor } from "./src/fleet/sensors";
import { parseGlossaryYaml } from "./src/glossary/parse";
import type { GlossaryTerm } from "./src/glossary/types";

const root = path.dirname(fileURLToPath(import.meta.url));

function loadGlossary(): { source: string; note: string; terms: GlossaryTerm[] } {
  const yamlPath = path.resolve(root, "../data/glossary.yaml");
  if (!fs.existsSync(yamlPath)) {
    throw new Error("data/glossary.yaml is required. The console does not keep a second glossary.");
  }
  return {
    source: "data/glossary.yaml",
    note: "",
    terms: parseGlossaryYaml(fs.readFileSync(yamlPath, "utf8")),
  };
}

function loadFleetSensors(): { source: string; note: string; sensors: FleetSensor[] } {
  const dir = path.resolve(root, "../data/fleet");
  if (!fs.existsSync(dir)) {
    throw new Error("data/fleet is required. The console does not keep a second sensor catalog.");
  }
  const sensors = fs
    .readdirSync(dir)
    .filter((name) => name.endsWith(".yaml") || name.endsWith(".yml"))
    .flatMap((name) => parseFleetSensors(fs.readFileSync(path.join(dir, name), "utf8")));
  if (sensors.length === 0) {
    throw new Error("data/fleet has no sensors. The console does not keep a second sensor catalog.");
  }
  return { source: "data/fleet", note: "", sensors };
}

function fleetSensorPlugin(): Plugin {
  const virtualId = "virtual:fleet-sensors";
  const resolvedId = "\0virtual:fleet-sensors";
  return {
    name: "mps-fleet-sensors",
    resolveId(id) {
      if (id === virtualId) return resolvedId;
      return null;
    },
    load(id) {
      if (id !== resolvedId) return null;
      const loaded = loadFleetSensors();
      return `export const fleetSensorSource = ${JSON.stringify(loaded.source)};\nexport const fleetSensorNote = ${JSON.stringify(loaded.note)};\nexport const fleetSensors = ${JSON.stringify(loaded.sensors)};\n`;
    },
  };
}

function glossaryPlugin(): Plugin {
  const virtualId = "virtual:glossary";
  const resolvedId = "\0virtual:glossary";
  return {
    name: "mps-glossary",
    resolveId(id) {
      if (id === virtualId) return resolvedId;
      return null;
    },
    load(id) {
      if (id !== resolvedId) return null;
      const loaded = loadGlossary();
      return `export const glossarySource = ${JSON.stringify(loaded.source)};\nexport const glossaryNote = ${JSON.stringify(loaded.note)};\nexport const glossaryTerms = ${JSON.stringify(loaded.terms)};\n`;
    },
  };
}

export default defineConfig({
  plugins: [react(), glossaryPlugin(), fleetSensorPlugin()],
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
    exclude: ["e2e/**", "**/node_modules/**", "**/dist/**"],
    include: ["src/**/*.{test,spec}.{ts,tsx}"],
  },
});
