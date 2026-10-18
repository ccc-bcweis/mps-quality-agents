// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ConsoleCatalog } from "../api/types";
import { limits as bands } from "../spc/series";
import fixture from "../test/catalog.fixture.json";
import { catalogLoaded, keepLoadingCatalog, limits, plants, ruleLabel, setCatalog } from ".";

const empty: ConsoleCatalog = { limits: null, rules: [], parameters: [], sites: [] };

afterEach(() => {
  vi.useRealTimers();
});

describe("the console catalog after a failed request (#340)", () => {
  it("keeps retrying until it lands, then every consumer has the catalog values", async () => {
    vi.useFakeTimers();
    setCatalog(empty);
    const fetcher = vi
      .fn<() => Promise<ConsoleCatalog>>()
      .mockRejectedValueOnce(new Error("502 during a deploy"))
      .mockRejectedValueOnce(new Error("502 during a deploy"))
      .mockResolvedValue(fixture as ConsoleCatalog);

    const stop = keepLoadingCatalog(fetcher, [1000, 2000]);
    await vi.advanceTimersByTimeAsync(0);
    expect(catalogLoaded()).toBe(false);
    expect(Number.isNaN(bands(10, 2).ucl3)).toBe(true);

    await vi.advanceTimersByTimeAsync(1000);
    expect(fetcher).toHaveBeenCalledTimes(2);
    await vi.advanceTimersByTimeAsync(2000);
    expect(fetcher).toHaveBeenCalledTimes(3);
    expect(catalogLoaded()).toBe(true);

    // The consumers a failed load used to blank: chart bands, override minimum, plants, rule sentences.
    expect(bands(10, 2).ucl3).toBe(10 + fixture.limits.zoneSigmas[2] * 2);
    expect(limits()?.overrideMinChars).toBe(fixture.limits.overrideMinChars);
    expect(plants().map((plant) => plant.id)).toEqual(fixture.sites.map((site) => site.id));
    expect(ruleLabel("western-electric-1")).toBe(fixture.rules[0].label);

    await vi.advanceTimersByTimeAsync(60000);
    expect(fetcher).toHaveBeenCalledTimes(3);
    stop();
  });

  it("stops retrying once the caller is gone", async () => {
    vi.useFakeTimers();
    setCatalog(empty);
    const fetcher = vi.fn<() => Promise<ConsoleCatalog>>().mockRejectedValue(new Error("offline"));
    const stop = keepLoadingCatalog(fetcher, [1000]);
    await vi.advanceTimersByTimeAsync(0);
    stop();
    await vi.advanceTimersByTimeAsync(10000);
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
});
