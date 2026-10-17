// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Guard: spec windows, control limits, rule numbers, rule sentences, parameter names and plants come from the server
 * (recipe data and /api/metadata/catalog, both read from data/), never from a literal in UI code. Fails on a number
 * range with a process unit, a sigma or count inside text ("3σ", "2 of 3", "8 readings in a row"), a threshold constant,
 * a literal sigma multiplier, a second stage/category label map, a parameter label map, or a plant name or tool prefix.
 * Comments, tests and the offline mocks are skipped. A legitimate constant is listed, with its reason, in
 * tools/guards/hardcoded-allowlist.txt (shared with tests/Simulator.Tests/NoHardcodedLimitsTests.cs).
 */
const SRC = path.resolve(__dirname, "..");
const REPO = path.resolve(SRC, "..", "..");
const LABELS_MODULE = path.join("labels", "index.ts");

const read = (relative: string) => fs.readFileSync(path.join(REPO, relative), "utf8");
const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

const parameterIds = [...read("data/metadata/parameters.yaml").matchAll(/id:\s*(\w+)/g)].map((match) => match[1]);
const sitesYaml = read("data/fleet/sites.yaml");
const plantNames = [...sitesYaml.matchAll(/^ {2}- id: \w+\s*\n\s+name: (.+)$/gm)].map((match) => match[1].trim());
const toolPrefixes = [...sitesYaml.matchAll(/toolPrefix: "([^"]+)"/g)].map((match) => match[1]);

type Pattern = { name: string; pattern: RegExp; except?: string };

const PATTERNS: Pattern[] = [
  { name: "unit range", pattern: /\d(?:\.\d+)?\s?[–-]\s?\d+(?:\.\d+)?\s?(?:°C|Torr|mTorr|sccm|kW|W|mm\/s|nm)\b/ },
  { name: "adder limit", pattern: /≤\s?\d+\s?adders/ },
  { name: "limit field", pattern: /\b(?:specLow|specHigh|controlLow|controlHigh|minimum|maximum|mean|sigma|endValue|nominal)\s*:\s*-?\d/ },
  { name: "value with unit", pattern: /\bvalue:\s*"[\d.]+\s?(?:°C|Torr|mTorr|sccm|kW|W|mm\/s|nm)/ },
  { name: "sigma in text", pattern: /\b\d+(?:\.\d+)?\s?(?:σ|sigma\b)|σ\d/i },
  { name: "count in text", pattern: /\b\d+ of (?:the last )?\d+\b|\b\d+ (?:readings|points|samples) in a row/ },
  {
    name: "threshold constant",
    pattern: /\bconst\s+[A-Z][A-Z0-9_]*(?:SIGMA|READING|THRESHOLD|LIMIT|MIN|MAX|COUNT|MINUTES|SECONDS|WINDOW|SHARE|RATIO|ATTEMPTS)[A-Z0-9_]*\s*=\s*-?\d/,
  },
  { name: "sigma multiplier", pattern: /\b\d+(?:\.\d+)?\s*\*\s*(?:\w+\.)?sigma\b|\bsigma\s*\*\s*\d/i },
  {
    name: "second label map",
    pattern: /Record<(?:CapaStage|IncidentStage|InvestigationStage|RootCauseCategory|IncidentChannel|ToolFamily),\s*string>/,
    except: LABELS_MODULE,
  },
  { name: "parameter label map", pattern: new RegExp(`\\b(?:${parameterIds.map(escape).join("|")})\\s*:\\s*"`) },
  { name: "plant name or tool prefix", pattern: new RegExp(`(?:${[...plantNames, ...toolPrefixes.map((prefix) => `"${prefix}"`)].map(escape).join("|")})`) },
];

type AllowEntry = { file: string; text: string; why: string };

function allowlist(): AllowEntry[] {
  return read("tools/guards/hardcoded-allowlist.txt")
    .split("\n")
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith("#"))
    .map((line) => {
      const [file = "", text = "", why = ""] = line.split("|").map((part) => part.trim());
      if (!file || !text || !why) throw new Error(`Allowlist line needs "path | text | why": ${line}`);
      return { file, text, why };
    });
}

function files(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === "mocks" || entry.name === "test" ? [] : files(full);
    return /\.(ts|tsx)$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) ? [full] : [];
  });
}

function scan(code: string, relative: string): string[] {
  return PATTERNS.filter((item) => item.except !== relative && item.pattern.test(code)).map((item) => item.name);
}

describe("no hard-coded limits in UI code", () => {
  it("has no literal limit, rule number, rule sentence, label map, parameter name or plant", () => {
    const allow = allowlist().filter((entry) => entry.file.startsWith("web/src/"));
    const hits: string[] = [];
    for (const file of files(SRC)) {
      const relative = path.relative(SRC, file);
      const repoRelative = path.relative(REPO, file).split(path.sep).join("/");
      fs.readFileSync(file, "utf8")
        .split("\n")
        .forEach((line, index) => {
          const code = line.trim();
          if (code.startsWith("//") || code.startsWith("*") || code.startsWith("/*")) return;
          if (allow.some((entry) => entry.file === repoRelative && code.includes(entry.text))) return;
          for (const name of scan(code, relative)) hits.push(`${repoRelative}:${index + 1}: [${name}] ${code}`);
        });
    }
    expect(hits).toEqual([]);
  });

  it("lists only allowlist entries that still match a line", () => {
    const stale = allowlist()
      .filter((entry) => entry.file.startsWith("web/"))
      .filter((entry) => !fs.existsSync(path.join(REPO, entry.file)) || !read(entry.file).split("\n").some((line) => line.includes(entry.text)))
      .map((entry) => `${entry.file} | ${entry.text}`);
    expect(stale).toEqual([]);
  });

  it("catches the kinds of literal that used to slip through", () => {
    const relative = path.join("pages", "Example.tsx");
    for (const code of [
      'const WE1 = "Western Electric rules, rule 1: one point is more than 3σ from the average";',
      '"Active until 8 readings in a row are back in control"',
      "export const OVERRIDE_MIN = 20;",
      "ucl3: mean + 3 * sigma,",
      "export const CAPA_STAGE_LABEL: Record<CapaStage, string> = {",
      'cf4Flow: "CF4 flow",',
      '{ id: "ironwood", name: "Ironwood Fab" },',
      'if (toolId.startsWith("I-")) return "ironwood";',
      'label={{ value: "UCL 3σ" }}',
    ]) {
      expect(scan(code, relative), code).not.toEqual([]);
    }
    for (const code of ["ucl3: mean + outer * sigma,", "Override needs at least {limitText(limits()?.overrideMinChars)} characters", 'const known = current.parameters.find((item) => item.id === parameter)?.label;']) {
      expect(scan(code, relative), code).toEqual([]);
    }
  });

  it("keeps the stage, category and CAPA labels in one module", () => {
    const owners = files(SRC).filter((file) => /Record<(?:CapaStage|IncidentStage|InvestigationStage|RootCauseCategory),\s*string>/.test(fs.readFileSync(file, "utf8")));
    expect(owners.map((file) => path.relative(SRC, file))).toEqual([LABELS_MODULE]);
  });
});
