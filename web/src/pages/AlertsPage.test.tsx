// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

describe("alerts", () => {
  it("starts with an empty list", async () => {
    renderConsole("/alerts");
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.queryByText(matchCopy(/Western Electric rules \(WE-1\) · 1 point > 3σ/))).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "TKT-1044" })).not.toBeInTheDocument();
  });
});

describe("alerts list matches tool status", () => {
  const shared = "scenario-SC-3-shared";
  const twoAlarms = [
    { id: "a-e01", toolId: "E-01", siteId: "northwater", chamberId: "E-01-A", parameter: "cf4Flow", severity: "critical", rule: "western-electric-1", raisedAt: "2026-09-01T00:01:00Z", correlationId: shared, status: "active" },
    { id: "a-e02", toolId: "E-02", siteId: "northwater", chamberId: "E-02-A", parameter: "cf4Flow", severity: "critical", rule: "western-electric-1", raisedAt: "2026-09-01T00:01:00Z", correlationId: shared, status: "active" },
    { id: "a-old", toolId: "D-01", siteId: "northwater", chamberId: "D-01-A", parameter: "chamberPressure", severity: "high", rule: "western-electric-1", raisedAt: "2026-09-01T00:00:00Z", correlationId: "old", status: "cleared" },
  ];

  it("lists one row per active alert, even when two tools share a scenario, and no evidence section", async () => {
    server.use(http.get("/api/alerts", () => HttpResponse.json(twoAlarms)));
    renderConsole("/alerts");
    const list = await screen.findByRole("table", { name: "Alerts" });
    await waitFor(() => expect(within(list).getAllByRole("row").filter((r) => within(r).queryAllByRole("cell").length > 0)).toHaveLength(2));
    expect(list).toHaveTextContent("E-01");
    expect(list).toHaveTextContent("E-02");
    expect(list).not.toHaveTextContent("D-01");
    expect(screen.queryByText(/Evidence linked to these alerts/)).not.toBeInTheDocument();
  });

  it("counts the same active alerts on the Production line button", async () => {
    server.use(http.get("/api/alerts", () => HttpResponse.json(twoAlarms)));
    renderConsole("/");
    expect(await screen.findByRole("link", { name: "Open alerts (2)" })).toBeInTheDocument();
  });
});
