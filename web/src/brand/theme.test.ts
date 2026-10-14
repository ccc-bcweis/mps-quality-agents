// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
// @vitest-environment node
import { createRequire } from "node:module";
import { describe, expect, it } from "vitest";

const require = createRequire(import.meta.url);

describe("tailwind theme", () => {
  it("loads the MPS canvas, alarm, and mono face", () => {
    const config = require("../../tailwind.config.cjs") as {
      theme: { extend: { colors: { mps: { canvas: string; nominal: string; alarm: string } }; fontFamily: { mono: string[] } } };
    };
    expect(config.theme.extend.colors.mps.canvas).toBe("#0E1116");
    expect(config.theme.extend.colors.mps.nominal).toBe("#22D3EE");
    expect(config.theme.extend.colors.mps.alarm).toBe("#EF4444");
    expect(config.theme.extend.fontFamily.mono[0]).toBe("JetBrains Mono");
  });
});
