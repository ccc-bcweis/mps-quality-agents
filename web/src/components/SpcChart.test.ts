// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { axisNumber } from "./SpcChart";

describe("SPC axis labels", () => {
  it("rounds to the spread so labels stay short", () => {
    expect(axisNumber(88.882542, 2.1)).toBe("88.9");
    expect(axisNumber(60.288096, 0.4)).toBe("60.29");
    expect(axisNumber(1450.123, 40)).toBe("1450");
    expect(axisNumber(0.0123456, 0.002)).toBe("0.012");
  });
});
