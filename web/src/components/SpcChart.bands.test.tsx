// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { cloneElement, type ReactElement } from "react";
import { render } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { SpcChart } from "./SpcChart";

// jsdom has no layout, so give the chart a fixed size instead of measuring its container.
vi.mock("recharts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("recharts")>();
  return {
    ...actual,
    ResponsiveContainer: ({ children }: { children: ReactElement<{ width?: number; height?: number }> }) =>
      cloneElement(children, { width: 720, height: 240 }),
  };
});

const points = Array.from({ length: 40 }, (_, index) => ({ index, value: 0.6 + Math.sin(index) * 0.1 }));

describe("SpcChart limit lines and sigma bands", () => {
  it("draws UCL, LCL, the mean and the three sigma bands when the server sends a center", () => {
    const { container } = render(
      <SpcChart label="Pump vibration" unit="mm/s" center={{ mean: 0.6, sigma: 0.33 }} points={points} finding="No active SPC alert." />,
    );
    // Recharts ignores reference elements wrapped in a Fragment, which once left only the data line (#338).
    expect(container.querySelectorAll(".recharts-reference-line")).toHaveLength(3);
    expect(container.querySelectorAll(".recharts-reference-area")).toHaveLength(3);
    expect(container.querySelector(".recharts-reference-line")?.textContent).toMatch(/UCL 3σ/);
    expect(container.querySelectorAll(".recharts-line")).toHaveLength(1);
  });

  it("draws no limits for a series without a center", () => {
    const { container } = render(<SpcChart label="Pump vibration" unit="mm/s" points={points} finding="x" />);
    expect(container.querySelectorAll(".recharts-reference-line")).toHaveLength(0);
    expect(container.querySelectorAll(".recharts-reference-area")).toHaveLength(0);
    expect(container.querySelectorAll(".recharts-line")).toHaveLength(1);
  });
});
