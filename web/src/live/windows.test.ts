// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { LiveReading } from "../api/types";
import { overlayWindows } from "./overlay";

const base: LiveReading = { sensorId: "s", parameter: "escTemperature", chamberId: "E-02-A", unit: "C", at: "2026-10-16T22:16:10Z", value: 56.7, status: "alarm" };

describe("operating windows from data", () => {
  it("shows the recipe-step spec and control limits the server sent, with its state and °C", () => {
    const [row] = overlayWindows([{ ...base, specLow: 46, specHigh: 54, center: 50, controlLow: 46, controlHigh: 54, limitState: "outOfSpec" }]);
    expect(row.window).toBe("46.0–54.0 °C");
    expect(row.control).toBe("46.0–54.0 °C");
    expect(row.now).toContain("°C");
    expect(row.tone).toBe("alarm");
    expect(row.state).toBe("Out of spec");
  });

  it("in spec but out of control is amber, not red", () => {
    const [row] = overlayWindows([{ ...base, value: 53, specLow: 46, specHigh: 54, center: 50, controlLow: 47, controlHigh: 53, limitState: "outOfControl" }]);
    expect(row.tone).toBe("warn");
    expect(row.state).toBe("In spec, out of control");
  });

  it("has no row without a window from data", () => {
    expect(overlayWindows([base])).toEqual([]);
  });
});
