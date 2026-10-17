// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { act, screen, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

const hub = vi.hoisted(() => ({ listeners: new Set<(event: string, payload: unknown) => void>() }));

vi.mock("../live/fleetHub", () => ({
  useRecordEvents: (listener: (event: string, payload: unknown) => void) => {
    hub.listeners.add(listener);
  },
}));

describe("tool detail linked records", () => {
  it("adds a new ticket at the top when the fleet stream says one opened", async () => {
    const tickets = [{ id: "t-old", number: "TKT-1001", incidentId: "n-old", toolId: "E-02", title: "Older ticket", openedAt: "2026-09-01T00:00:00Z", status: "open" }];
    server.use(http.get("/api/tickets", () => HttpResponse.json(tickets)));
    renderConsole("/tools/E-02");
    const list = await screen.findByRole("list", { name: "Linked records" });
    expect(await within(list).findByText("Older ticket")).toBeInTheDocument();

    tickets.push({ id: "t-new", number: "TKT-1002", incidentId: "n-new", toolId: "E-02", title: "Newer ticket", openedAt: "2026-09-02T00:00:00Z", status: "open" });
    act(() => {
      for (const listener of hub.listeners) listener("ticketOpened", { toolId: "E-02" });
    });

    expect(await within(list).findByText("Newer ticket", {}, { timeout: 5000 })).toBeInTheDocument();
    const rows = within(screen.getByRole("list", { name: "Linked records" })).getAllByRole("listitem");
    const titles = rows.map((row) => row.textContent ?? "");
    const newer = titles.findIndex((text) => text.includes("Newer ticket"));
    const older = titles.findIndex((text) => text.includes("Older ticket"));
    expect(newer).toBeGreaterThanOrEqual(0);
    expect(newer).toBeLessThan(older);
  });
});
