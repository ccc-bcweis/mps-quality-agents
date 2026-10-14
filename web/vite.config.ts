// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig, type Plugin } from "vitest/config";
import { parseGlossaryYaml, termsFromGlossaryFile } from "./src/glossary/parse";
import type { GlossaryTerm } from "./src/glossary/types";

const root = path.dirname(fileURLToPath(import.meta.url));

function loadGlossary(): { source: string; note: string; terms: GlossaryTerm[] } {
  const yamlPath = path.resolve(root, "../data/glossary.yaml");
  const fallbackPath = path.resolve(root, "src/glossary.fallback.json");
  if (fs.existsSync(yamlPath)) {
    return {
      source: "data/glossary.yaml",
      note: "",
      terms: parseGlossaryYaml(fs.readFileSync(yamlPath, "utf8")),
    };
  }
  const file = JSON.parse(fs.readFileSync(fallbackPath, "utf8")) as { sourceNote?: string };
  return {
    source: "web/src/glossary.fallback.json",
    note: file.sourceNote ?? "",
    terms: termsFromGlossaryFile(file),
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
  plugins: [react(), glossaryPlugin()],
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
