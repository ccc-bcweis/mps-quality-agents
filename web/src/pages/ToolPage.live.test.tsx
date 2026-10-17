// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { act, screen, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { emitHubEvent, setHubConnected, watchedGroups } from "../live/fleetHub";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

const T0 = "2026-09-01T00:00:00Z";

function counted(path: string, body: () => Parameters<typeof HttpResponse.json>[0]): { count: () => number } {
  let calls = 0;
  server.use(
    http.get(path, () => {
      calls += 1;
      return HttpResponse.json(body());
    }),
  );
  return { count: () => calls };
}

function liveBody(points: { at: string; value: number }[]) {
  return {
    toolId: "E-01",
    siteId: "northwater",
    simulatedNow: T0,
    status: "nominal",
    readings: [],
    series: points.length === 0 ? [] : [{ parameter: "etchRate", chamberId: "E-01-A", center: 350, sigma: 20, points }],
  };
}

describe("tool detail on the fleet stream", () => {
  it("patches a new ticket in at the top without downloading the ticket list again", async () => {
    const tickets = counted("/api/tickets", () => [
      { id: "t-old", number: "TKT-1001", incidentId: "n-old", toolId: "E-02", title: "Older ticket", openedAt: T0, status: "open" },
    ]);
    renderConsole("/tools/E-02");
    const list = await screen.findByRole("list", { name: "Linked records" });
    expect(await within(list).findByText("Older ticket")).toBeInTheDocument();
    const loads = tickets.count();

    act(() => {
      emitHubEvent("ticketOpened", { id: "t-new", number: "TKT-1002", incidentId: "n-new", toolId: "E-02", title: "Newer ticket", openedAt: "2026-09-02T00:00:00Z", status: "open" });
      emitHubEvent("ticketOpened", { id: "t-other", number: "TKT-1003", incidentId: "n-other", toolId: "D-01", title: "Other tool ticket", openedAt: "2026-09-02T00:00:00Z", status: "open" });
    });

    expect(await within(list).findByText("Newer ticket")).toBeInTheDocument();
    expect(within(list).queryByText("Other tool ticket")).not.toBeInTheDocument();
    const titles = within(screen.getByRole("list", { name: "Linked records" }))
      .getAllByRole("listitem")
      .map((row) => row.textContent ?? "");
    expect(titles.findIndex((text) => text.includes("Newer ticket"))).toBeLessThan(titles.findIndex((text) => text.includes("Older ticket")));
    expect(tickets.count()).toBe(loads);
  });

  it("waits for readings, then draws the points the toolLive stream appends, with no extra load", async () => {
    const live = counted("/api/tools/:toolId/live", () => liveBody([]));
    renderConsole("/tools/E-01?parameter=etchRate");
    expect(await screen.findByRole("figure", { name: /Etch rate\. Waiting for readings/ })).toBeInTheDocument();
    expect(watchedGroups()).toContain("SubscribeTool|E-01");
    const loads = live.count();

    act(() => {
      emitHubEvent("toolLive", {
        ...liveBody([
          { at: "2026-09-01T00:00:15Z", value: 351 },
          { at: "2026-09-01T00:00:30Z", value: 349 },
        ]),
        readings: [{ sensorId: "E-01-A-OUT-ETCH", parameter: "etchRate", chamberId: "E-01-A", unit: "nm/min", at: "2026-09-01T00:00:30Z", value: 349, status: "nominal" }],
        resync: false,
      });
    });

    expect(await screen.findByRole("figure", { name: /SPC chart of Etch rate\. Mean 350\.00/ })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /E-01-A-OUT-ETCH/ })).toHaveTextContent("349");
    expect(screen.getByText(/n = 2/)).toBeInTheDocument();

    act(() => {
      emitHubEvent("toolLive", { ...liveBody([{ at: "2026-09-01T00:00:45Z", value: 352 }]), toolId: "D-01", resync: false });
      emitHubEvent("toolLive", { ...liveBody([{ at: "2026-09-01T00:00:45Z", value: 352 }]), resync: false });
    });
    expect(await screen.findByText(/n = 3/)).toBeInTheDocument();
    expect(live.count()).toBe(loads);
  });

  it("loads the tool again on resync and after a reconnect", async () => {
    const live = counted("/api/tools/:toolId/live", () => liveBody([{ at: T0, value: 350 }]));
    renderConsole("/tools/E-01?parameter=etchRate");
    expect(await screen.findByRole("figure", { name: /SPC chart of Etch rate/ })).toBeInTheDocument();
    const loads = live.count();

    act(() => emitHubEvent("toolLive", { ...liveBody([]), resync: true }));
    await screen.findByRole("figure", { name: /SPC chart of Etch rate/ });
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    expect(live.count()).toBe(loads + 1);

    act(() => setHubConnected(true, true));
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    expect(live.count()).toBe(loads + 2);
  });

  it("states the server's finding for the charted series and marks a cleared alert", async () => {
    const alert = { id: "a-1", toolId: "E-01", chamberId: "E-01-A", parameter: "etchRate", severity: "high", rule: "western-electric-1", raisedAt: T0, correlationId: "c-1", status: "active" };
    server.use(http.get("/api/alerts", () => HttpResponse.json([alert])));
    counted("/api/tools/:toolId/live", () => liveBody([{ at: T0, value: 250 }]));
    renderConsole("/tools/E-01?parameter=etchRate");
    expect(await screen.findByRole("figure", { name: /Active alert: Western Electric/ })).toBeInTheDocument();

    act(() => emitHubEvent("alertCleared", { alertId: "a-1", toolId: "E-01", clearedAt: "2026-09-01T00:05:00Z" }));

    expect(await screen.findByRole("figure", { name: /No active SPC alert on this series/ })).toBeInTheDocument();
  });
});
