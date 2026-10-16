// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

/**
 * Guard: spec windows, control limits and thresholds come from the recipe data the server sends, never from a literal
 * in UI code. Fails on a number range with a process unit ("40–60 °C", "2-6 Torr") or an "≤10 adders" style limit
 * outside comments, tests and the offline mocks.
 */
const SRC = path.resolve(__dirname, "..");
const LIMIT_LITERAL = [
  /\d(?:\.\d+)?\s?[–-]\s?\d+(?:\.\d+)?\s?(?:°C|Torr|mTorr|sccm|kW|W|mm\/s|nm)\b/,
  /≤\s?\d+\s?adders/,
  /\b(?:specLow|specHigh|controlLow|controlHigh|minimum|maximum|mean|sigma|endValue|nominal)\s*:\s*-?\d/,
  /\bvalue:\s*"[\d.]+\s?(?:°C|Torr|mTorr|sccm|kW|W|mm\/s|nm)/,
];

function files(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return entry.name === "mocks" || entry.name === "test" ? [] : files(full);
    return /\.(ts|tsx)$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) ? [full] : [];
  });
}

describe("no hard-coded limits in UI code", () => {
  it("has no literal spec window, control limit or adder threshold", () => {
    const hits: string[] = [];
    for (const file of files(SRC)) {
      fs.readFileSync(file, "utf8")
        .split("\n")
        .forEach((line, index) => {
          const code = line.trim();
          if (code.startsWith("//") || code.startsWith("*") || code.startsWith("/*")) return;
          if (LIMIT_LITERAL.some((pattern) => pattern.test(code))) hits.push(`${path.relative(SRC, file)}:${index + 1}: ${code}`);
        });
    }
    expect(hits).toEqual([]);
  });

  it("keeps one parameter label map, shared with the server", () => {
    const owners = files(SRC).filter((file) => /const PARAMETER_LABEL\b/.test(fs.readFileSync(file, "utf8")));
    expect(owners.map((file) => path.relative(SRC, file))).toEqual([path.join("sites", "parameters.ts")]);
  });
});
