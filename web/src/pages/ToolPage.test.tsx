// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";
import { matchCopy } from "../test/matchCopy";
import { server } from "../test/server";

describe("tool detail", () => {
  it("draws an SPC chart and the etch windows for E-02", async () => {
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("heading", { name: /Tool E-02/ })).toBeInTheDocument();
    const chart = await screen.findByRole("figure", { name: /SPC chart of ESC temperature/ });
    expect(chart).toHaveAccessibleName(/SPC chart of ESC temperature/i);
    expect(screen.getByText("No live readings with a recipe window yet.")).toBeInTheDocument();
    expect(screen.queryByText("40–60 °C")).not.toBeInTheDocument();
    expect(screen.queryByText("53.1 °C osc")).not.toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Tool performance" })).toHaveTextContent("24.0 wafers/h");
    expect(screen.getByText("Illustrative readings, not a live tool counter.")).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/mean time between failures \(MTBF\)/))).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/mean time to repair \(MTTR\)/))).toBeInTheDocument();
  });

  it("binds etch sensors to the schematic and opens the chart for the point that was chosen", async () => {
    const user = userEvent.setup();
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("region", { name: /plasma etch schematic/i })).toBeInTheDocument();
    const esc = screen.getByRole("button", { name: /E-02-A-TC-ESC/ });
    expect(esc).not.toHaveTextContent("53.1");
    expect(esc).toHaveTextContent("C");
    expect(esc).toHaveAttribute("data-status", "inSpec");
    expect(esc).toHaveAttribute("data-anchor", "a.chuck.esc");
    expect(screen.getByRole("button", { name: /E-02-A-OUT-ENDPOINT/ })).toHaveAttribute("data-anchor", "a.chamber.endpoint");
    expect(screen.queryByText(/is not on the drawing/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /E-02-A-PT-CHAMBER/ }));
    expect(await screen.findByRole("figure", { name: /SPC chart of Chamber pressure/ })).toBeInTheDocument();
  });

  it("keeps deposition chamber pressure inside the window", async () => {
    renderConsole("/tools/D-01");
    const pressure = await screen.findByRole("button", { name: /D-01-A-PT-CHAMBER/ });
    expect(pressure).not.toHaveTextContent("6.52");
    expect(pressure).toHaveTextContent("Torr");
    expect(pressure).toHaveAttribute("data-status", "inSpec");
    expect(screen.getAllByText("4.10 Torr").length).toBeGreaterThan(0);
    expect(screen.getByRole("region", { name: /thin-film deposition schematic/i })).toBeInTheDocument();
  });

  it("draws the strip schematic with remote plasma power in watts", async () => {
    renderConsole("/tools/S-01");
    expect(await screen.findByRole("region", { name: /photoresist strip schematic/i })).toBeInTheDocument();
    const remote = screen.getByRole("button", { name: /S-01-A-RF-REMOTE-FWD/ });
    expect(remote).toHaveTextContent("W");
    expect(remote.textContent).not.toMatch(/kW/);
  });

  it("draws the clean schematic without an RF generator", async () => {
    renderConsole("/tools/C-01");
    expect(await screen.findByRole("region", { name: /single-wafer clean schematic/i })).toBeInTheDocument();
    const megasonic = screen.getByRole("button", { name: /C-01-A-MEG-PWR/ });
    expect(megasonic).toHaveTextContent("W");
    expect(screen.queryByRole("button", { name: /RF FWD/ })).not.toBeInTheDocument();
  });

  it("says when a tool id is unknown", async () => {
    renderConsole("/tools/Z-99");
    expect(await screen.findByText("Tool was not found.")).toBeInTheDocument();
  });

  it("plots a streaming series and colors the matching sensor", async () => {
    server.use(
      http.get("/api/tools/:toolId/live", () =>
        HttpResponse.json({
          toolId: "E-01",
          siteId: "northwater",
          simulatedNow: "2026-09-01T00:10:00Z",
          status: "alarm",
          readings: [
            {
              sensorId: "E-01-A-OUT-ETCH",
              parameter: "etchRate",
              chamberId: "E-01-A",
              unit: "nm/min",
              at: "2026-09-01T00:10:00Z",
              value: 250,
              status: "alarm",
            },
          ],
          series: [
            {
              parameter: "etchRate",
              chamberId: "E-01-A",
              center: 350,
              sigma: 20,
              points: Array.from({ length: 12 }, (_, index) => ({
                at: "2026-09-01T00:00:00Z",
                value: index < 8 ? 350 : 250,
              })),
            },
          ],
        }),
      ),
    );
    renderConsole("/tools/E-01?streaming=1&parameter=etchRate");
    expect(await screen.findByRole("status", { name: "Streaming trace" })).toHaveTextContent(/streaming/i);
    expect(await screen.findByRole("figure", { name: /SPC chart of Etch rate/ })).toHaveAccessibleName(/beyond 3 sigma/i);
    const etch = screen.getByRole("button", { name: /E-01-A-OUT-ETCH/ });
    expect(etch).toHaveAttribute("data-status", "outOfSpec");
    expect(etch).toHaveTextContent("250");
    expect(screen.getByRole("heading", { name: /Tool E-01/ })).toBeInTheDocument();
  });

  it("draws a clone tool from its template sensors", async () => {
    renderConsole("/tools/D-03");
    expect(await screen.findByRole("heading", { name: /Tool D-03/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /D-03-A-PT-CHAMBER/ })).toHaveAttribute("data-anchor", "a.chamber.pressure");
    expect(screen.queryByRole("button", { name: /D-01-A-PT-CHAMBER/ })).not.toBeInTheDocument();
  });
});

describe("tool detail with a manual reading", () => {
  it("shows the printout on the tile, the chart, the status reason and linked records", async () => {
    server.use(
      http.get("/api/manual-readings", () =>
        HttpResponse.json([
          {
            evidenceId: "77777777-7777-4777-8777-777777777777",
            incidentId: "66666666-6666-4666-8666-666666666666",
            alertId: "44444444-4444-4444-8444-444444444444",
            toolId: "E-02",
            siteId: "northwater",
            chamberId: "E-02-A",
            parameter: "escTemperature",
            instrument: "thermocoupleLogger",
            value: 68,
            unit: "C",
            specMinimum: 40,
            specMaximum: 60,
            outOfSpec: true,
            observedAt: "2026-08-29T09:40:00Z",
            printedObservedAt: null,
            olderThanWindow: true,
            active: true,
            operatorInitials: "JL",
            fileName: "thermocouple-logger-e01.png",
            fileUrl: "/api/manual-readings/77777777-7777-4777-8777-777777777777/file",
            source: "Manual reading from the thermocouple logger printout thermocouple-logger-e01.png, operator JL: 68 C (spec 40-60), out of spec.",
          },
        ]),
      ),
      http.get("/api/alerts", () =>
        HttpResponse.json([
          {
            id: "44444444-4444-4444-8444-444444444444",
            toolId: "E-02",
            siteId: "northwater",
            chamberId: "E-02-A",
            parameter: "escTemperature",
            severity: "high",
            rule: "WE1",
            raisedAt: "2026-09-01T00:01:00Z",
            correlationId: "printout-1",
            status: "active",
          },
        ]),
      ),
      http.get("/api/tools/:toolId/live", () =>
        HttpResponse.json({
          toolId: "E-02",
          siteId: "northwater",
          simulatedNow: "2026-09-01T00:02:00Z",
          status: "alarm",
          readings: [{ sensorId: "E-02-A-ESC-TEMP", parameter: "escTemperature", chamberId: "E-02-A", unit: "C", at: "2026-09-01T00:00:15Z", value: 51, status: "nominal" }],
          series: [
            {
              parameter: "escTemperature",
              chamberId: "E-02-A",
              center: 50,
              sigma: 3,
              points: [
                { at: "2026-09-01T00:00:00Z", value: 50 },
                { at: "2026-09-01T00:00:15Z", value: 51 },
              ],
            },
          ],
        }),
      ),
    );
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("heading", { name: /Why E-02 shows alarm/ })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Why this status" })).toHaveTextContent(/thermocouple logger printout/);
    expect(document.querySelector("[data-manual-badge]")).not.toBeNull();
    expect(await screen.findByText(/Manual reading 68 C \(thermocouple logger\)\. Observed before this window/)).toBeInTheDocument();
    const linked = screen.getByRole("list", { name: "Linked records" });
    expect(linked).toHaveTextContent(/Older than the chart window/);
    expect(screen.getByRole("link", { name: "Printout" })).toHaveAttribute("href", "/api/manual-readings/77777777-7777-4777-8777-777777777777/file");
  });
});

describe("tool detail after the manual reading is resolved", () => {
  it("shows the live tile, no alarm reason, and the marker as resolved history", async () => {
    server.use(
      http.get("/api/manual-readings", () =>
        HttpResponse.json([
          {
            evidenceId: "77777777-7777-4777-8777-777777777777",
            incidentId: "66666666-6666-4666-8666-666666666666",
            alertId: "44444444-4444-4444-8444-444444444444",
            toolId: "E-02",
            siteId: "northwater",
            chamberId: "E-02-A",
            parameter: "escTemperature",
            instrument: "thermocoupleLogger",
            value: 68,
            unit: "C",
            specMinimum: 40,
            specMaximum: 60,
            outOfSpec: true,
            observedAt: "2026-09-01T00:00:10Z",
            printedObservedAt: "2026-08-29T09:40:00Z",
            olderThanWindow: false,
            active: false,
            operatorInitials: "JL",
            fileName: "thermocouple-logger-e01.png",
            fileUrl: "/api/manual-readings/77777777-7777-4777-8777-777777777777/file",
            source: "Manual reading from the thermocouple logger printout thermocouple-logger-e01.png, operator JL: 68 C (spec 40-60), out of spec.",
          },
        ]),
      ),
      http.get("/api/alerts", () =>
        HttpResponse.json([
          {
            id: "44444444-4444-4444-8444-444444444444",
            toolId: "E-02",
            siteId: "northwater",
            chamberId: "E-02-A",
            parameter: "escTemperature",
            severity: "high",
            rule: "WE1",
            raisedAt: "2026-09-01T00:00:10Z",
            correlationId: "c-1",
            status: "cleared",
            clearedAt: "2026-09-01T00:01:30Z",
          },
        ]),
      ),
      http.get("/api/tools/:toolId/live", () =>
        HttpResponse.json({
          toolId: "E-02",
          siteId: "northwater",
          simulatedNow: "2026-09-01T00:02:00Z",
          status: "nominal",
          readings: [{ sensorId: "E-02-A-ESC-TEMP", parameter: "escTemperature", chamberId: "E-02-A", unit: "C", at: "2026-09-01T00:00:15Z", value: 51, status: "nominal" }],
          series: [
            {
              parameter: "escTemperature",
              chamberId: "E-02-A",
              center: 50,
              sigma: 3,
              points: [
                { at: "2026-09-01T00:00:00Z", value: 50 },
                { at: "2026-09-01T00:00:15Z", value: 51 },
              ],
            },
          ],
        }),
      ),
    );
    renderConsole("/tools/E-02");
    expect(await screen.findByText(/Manual reading 68 C \(thermocouple logger\), resolved/)).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /Why E-02 shows alarm/ })).not.toBeInTheDocument();
    expect(document.querySelector("[data-manual-badge]")).toBeNull();
  });
});
