// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { Tool } from "../api/types";
import { storyFor } from "./catalog";

const tool = { id: "Z-99", chambers: [] } as unknown as Tool;

describe("tool story", () => {
  it("says a fault is active only while an alert is active", () => {
    expect(storyFor(tool, [{ toolId: "Z-99", status: "active" }]).note).toBe("A fault is active on this tool.");
    expect(storyFor(tool, [{ toolId: "Z-99", status: "cleared" }]).note).not.toMatch(/fault is active/);
  });
});
