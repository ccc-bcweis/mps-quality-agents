// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import * as telemetry from "./telemetry";
import { formatValue } from "./telemetry";

describe("sensor value display", () => {
  it("rounds each unit to its own decimals", () => {
    expect(formatValue(6.523, "Torr")).toBe("6.52");
    expect(formatValue(53.14, "C")).toBe("53.1");
    expect(formatValue(1450.4, "W")).toBe("1450");
  });

  it("has no frozen or generated readings to fall back on", () => {
    expect(Object.keys(telemetry).sort()).toEqual(["formatValue"]);
  });
});
