// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";
import { matchCopy } from "../test/matchCopy";
import { server } from "../test/server";

describe("fleet overview", () => {
  it("shows the eight-tool fab in a nominal state", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    expect(await screen.findByRole("heading", { name: "Fleet overview" })).toBeInTheDocument();
    for (const id of ["D-01", "D-02", "E-01", "E-02", "S-01", "S-02", "C-01", "C-02"]) {
      expect(screen.getByRole("link", { name: new RegExp(id) })).toBeInTheDocument();
    }
    const deposition = screen.getByRole("link", { name: /D-01/ });
    expect(deposition.textContent).toMatch(/NOMINAL/);
    expect(deposition.textContent).not.toMatch(/ALARM/);
    expect(deposition.textContent).not.toMatch(/wafers\/h/);
    expect(screen.getByRole("link", { name: /E-02/ }).textContent).toMatch(/NOMINAL/);
    expect(screen.getByRole("link", { name: /E-02/ }).textContent).not.toMatch(/WATCH/);
    expect(screen.getByRole("img", { name: "Meridian Precision Systems mark" })).toBeInTheDocument();
    expect(screen.getByText("Powered by StatusNeo")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Fleet performance" })).toHaveTextContent("Illustrative readings, not a live tool counter.");
    expect(screen.getByText("26.4 wafers/h")).toBeInTheDocument();
    expect(screen.getByText("78.4%")).toBeInTheDocument();
    expect(screen.getByText("22.0 wafers/h")).toBeInTheDocument();
    const oee = screen.getByText(matchCopy(/overall equipment effectiveness \(OEE\)/));
    await user.click(oee);
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/good-wafer rate/i);
  });

  it("replaces a tile when that tool has a live reading", async () => {
    server.use(
      http.get("/api/fleet/live", () =>
        HttpResponse.json({
          simulatedNow: "2026-09-01T00:04:00Z",
          tools: [
            {
              toolId: "D-02",
              siteId: "northwater",
              status: "alarm",
              readings: [
                {
                  sensorId: "D-02-A-PT-CHAMBER",
                  parameter: "chamberPressure",
                  chamberId: "D-02-A",
                  unit: "Torr",
                  at: "2026-09-01T00:04:00Z",
                  value: 9.5,
                  status: "alarm",
                },
              ],
            },
          ],
        }),
      ),
    );
    renderConsole("/");
    await waitFor(() => {
      expect(screen.getByRole("link", { name: /D-02/ }).textContent).toMatch(/ALARM/);
    });
    expect(screen.getByRole("link", { name: /D-02/ }).textContent).toMatch(/9\.50/);
    expect(screen.getByRole("link", { name: /E-02/ }).textContent).toMatch(/NOMINAL/);
    expect(screen.getByRole("link", { name: /D-01/ }).textContent).toMatch(/NOMINAL/);
  });
});
