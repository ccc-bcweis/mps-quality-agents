// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { pollUntil } from "./poll";

describe("pollUntil", () => {
  it("returns the first ready value without waiting", async () => {
    const sleeps: number[] = [];
    const value = await pollUntil(
      async () => 1,
      (item) => item === 1,
      {
        timeoutMs: 10_000,
        intervalMs: 400,
        sleep: async (ms) => {
          sleeps.push(ms);
        },
        now: () => 0,
      },
    );
    expect(value).toBe(1);
    expect(sleeps).toEqual([]);
  });

  it("gives up when the timeout is already due", async () => {
    let calls = 0;
    const value = await pollUntil(
      async () => {
        calls += 1;
        return "pending";
      },
      () => false,
      { timeoutMs: 0, intervalMs: 10, sleep: async () => undefined, now: () => 5 },
    );
    expect(value).toBeNull();
    expect(calls).toBe(1);
  });
});
