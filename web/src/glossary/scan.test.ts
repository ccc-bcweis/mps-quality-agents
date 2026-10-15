// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { glossaryNote, glossarySource, glossaryTerms } from "virtual:glossary";
import { missingAcronyms, REQUIRED_TERMS, stringLiterals } from "./scan";

const here = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(here, "../..");
const repoRoot = path.resolve(webRoot, "..");

const SKIP = new Set([
  path.normalize("src/api/client.ts"),
  path.normalize("src/api/types.ts"),
  path.normalize("src/mocks/handlers.ts"),
  path.normalize("src/mocks/browser.ts"),
  path.normalize("src/mocks/state.ts"),
]);

function copyFiles(dir: string, out: string[]) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "node_modules" || entry.name === "dist") continue;
      copyFiles(full, out);
      continue;
    }
    if (!/\.(ts|tsx)$/.test(entry.name) || /\.test\.(ts|tsx)$/.test(entry.name)) continue;
    const relative = path.normalize(path.relative(webRoot, full));
    if (SKIP.has(relative)) continue;
    out.push(full);
  }
}

function uiCopyHits(): { token: string; file: string }[] {
  const files: string[] = [];
  copyFiles(path.join(webRoot, "src"), files);
  const hits: { token: string; file: string }[] = [];
  for (const file of files) {
    const source = fs.readFileSync(file, "utf8");
    const missing = missingAcronyms(stringLiterals(source).join("\n"), glossaryTerms);
    for (const token of missing) hits.push({ token, file: path.relative(webRoot, file) });
  }
  return hits;
}

describe("glossary coverage", () => {
  it("loads data/glossary.yaml as the only glossary", () => {
    const yamlPath = path.join(repoRoot, "data", "glossary.yaml");
    expect(fs.existsSync(yamlPath)).toBe(true);
    expect(fs.existsSync(path.join(webRoot, "src", "glossary.fallback.json"))).toBe(false);
    expect(glossarySource).toBe("data/glossary.yaml");
    expect(glossaryNote).toBe("");
    expect(glossaryTerms.length).toBeGreaterThan(REQUIRED_TERMS.length);
  });

  it("includes every acronym the specification names", () => {
    const known = new Set(glossaryTerms.map((term) => term.term));
    const missing = REQUIRED_TERMS.filter((term) => !known.has(term));
    expect(missing).toEqual([]);
  });

  it("fails when UI copy uses an acronym that is not in the glossary", () => {
    expect(missingAcronyms("The ZZZ chart drifted.", glossaryTerms)).toContain("ZZZ");
    expect(stringLiterals('const pattern = /("REQUIRED")/g;\nconst label = "ZZZ";')).toEqual(["ZZZ"]);
    expect(missingAcronyms("Recipe ETCH-OX-14, CAL-ESC-02, and NF3-220.", glossaryTerms)).toEqual([]);
    expect(missingAcronyms("#EF4444 AppShell ModeBanner SpcChart", glossaryTerms)).toEqual([]);
    expect(missingAcronyms("WE-1 and MTTR-Q stay in the glossary.", glossaryTerms)).toEqual([]);
    expect(missingAcronyms("WE-1 tripped.", [{ term: "SPC", expansion: "x", definition: "y" }])).toEqual(["WE-1"]);
    expect(missingAcronyms("WE-1 tripped.", [{ term: "WE", expansion: "x", definition: "y" }])).toEqual([]);
    const hits = uiCopyHits();
    expect(hits, hits.map((hit) => `${hit.token} in ${hit.file}`).join("\n")).toEqual([]);
  });
});
