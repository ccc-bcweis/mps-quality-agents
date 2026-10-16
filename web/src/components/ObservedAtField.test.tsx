// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { server } from "../test/server";
import { ObservedAtField } from "./ObservedAtField";

describe("observed time on a printout", () => {
  it("flags a time older than the chart window and Now sets the plant clock while keeping the printed time", async () => {
    server.use(
      http.get("/api/tools/:toolId/live", () =>
        HttpResponse.json({
          toolId: "E-01",
          siteId: "northwater",
          simulatedNow: "2026-10-15T23:05:00Z",
          status: "alarm",
          readings: [],
          series: [{ parameter: "escTemperature", chamberId: "E-01-A", points: [{ at: "2026-10-15T23:00:00Z", value: 50 }] }],
        }),
      ),
    );
    const onChange = vi.fn();
    render(<ObservedAtField toolId="E-01" chamberId="E-01-A" parameter="escTemperature" value="2026-10-12T09:40:00Z" onChange={onChange} />);
    expect(await screen.findByText(/older than the chart window/)).toBeInTheDocument();
    const now = screen.getByRole("button", { name: "Now" });
    expect(now.tagName).toBe("BUTTON");
    await userEvent.click(now);
    expect(onChange).toHaveBeenCalledWith("2026-10-15T23:05:00Z", "2026-10-12T09:40:00Z");
  });

  it("keeps showing the printed time after Now and no longer flags the reading", async () => {
    server.use(
      http.get("/api/tools/:toolId/live", () =>
        HttpResponse.json({
          toolId: "E-01",
          siteId: "northwater",
          simulatedNow: "2026-10-15T23:05:00Z",
          status: "alarm",
          readings: [],
          series: [{ parameter: "escTemperature", chamberId: "E-01-A", points: [{ at: "2026-10-15T23:00:00Z", value: 50 }] }],
        }),
      ),
    );
    render(
      <ObservedAtField
        toolId="E-01"
        chamberId="E-01-A"
        parameter="escTemperature"
        value="2026-10-15T23:05:00Z"
        printed="2026-10-12T09:40:00Z"
        onChange={() => undefined}
      />,
    );
    expect(await screen.findByText(/Printed time .* is kept on the record/)).toBeInTheDocument();
    expect(screen.queryByText(/older than the chart window/)).not.toBeInTheDocument();
  });
});
