// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AppRoutes } from "../App";
import { ConsoleProvider } from "../context/ConsoleContext";
import { S1_INCIDENT_ID, S1_INVESTIGATION_ID } from "../mocks/demoData";
import { SITE_STORAGE_KEY } from "../sites/selection";
import { server } from "../test/server";
import { renderConsole } from "../test/render";

const northwaterAlert = {
  id: "11111111-1111-4111-8111-1111111111a1",
  toolId: "E-01",
  parameter: "gasFlow",
  severity: "high",
  rule: "westernElectric1",
  raisedAt: "2026-10-14T15:00:00Z",
  correlationId: "corr-nw",
};

const ironwoodAlert = {
  id: "11111111-1111-4111-8111-1111111111a2",
  toolId: "I-E-01",
  parameter: "gasFlow",
  severity: "high",
  rule: "westernElectric1",
  raisedAt: "2026-10-14T15:01:00Z",
  correlationId: "corr-iw",
};

describe("site selection", () => {
  it("offers every plant in the header and wraps instead of clipping", async () => {
    renderConsole("/");
    const consoleRegion = await screen.findByRole("region", { name: "Meridian Precision Systems console" });
    const header = consoleRegion.querySelector("header");
    expect(header?.className).toContain("flex-wrap");
    expect(header?.className).not.toMatch(/truncate|overflow-hidden/);
    const plant = within(consoleRegion).getByRole("combobox", { name: "Plant" });
    expect(plant).toHaveValue("all");
    expect(plant.className).toContain("whitespace-nowrap");
    expect(plant.className).not.toMatch(/truncate|overflow-hidden|min-w-0/);
    expect(within(plant).getByRole("option", { name: "All sites" })).toBeInTheDocument();
    expect(within(plant).getByRole("option", { name: "Northwater Fab" })).toBeInTheDocument();
    expect(within(plant).getByRole("option", { name: "Ironwood Fab" })).toBeInTheDocument();
    expect(within(plant).getByRole("option", { name: "Lumen Pilot Works" })).toBeInTheDocument();
  });

  it("keeps a floor link from changing the plant until a card is clicked", async () => {
    const user = userEvent.setup();
    renderConsole("/sites?site=lumen");
    expect(await screen.findByRole("heading", { name: "Lumen Pilot Works" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Plant" })).toHaveValue("all");

    await user.selectOptions(screen.getByRole("combobox", { name: "Plant" }), "northwater");
    expect(await screen.findByRole("heading", { name: "Northwater Fab" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Plant" })).toHaveValue("northwater");

    await user.selectOptions(screen.getByRole("combobox", { name: "Plant" }), "all");
    expect(screen.getByRole("heading", { name: "Northwater Fab" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Plant" })).toHaveValue("all");
  });

  it("remembers the plant across console tabs, reload, and another browser tab", async () => {
    const user = userEvent.setup();
    const first = renderConsole("/sites");
    await user.click(await screen.findByRole("tab", { name: "Ironwood Fab" }));
    expect(screen.getByRole("combobox", { name: "Plant" })).toHaveValue("ironwood");
    expect(localStorage.getItem(SITE_STORAGE_KEY)).toBe("ironwood");
    expect(await screen.findByRole("link", { name: /Etch I-E-01/ })).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "Fleet" }));
    expect(await screen.findByText("No tools at this plant.")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /D-01/ })).not.toBeInTheDocument();

    first.unmount();
    render(
      <MemoryRouter initialEntries={["/"]}>
        <ConsoleProvider>
          <AppRoutes />
        </ConsoleProvider>
      </MemoryRouter>,
    );
    expect(await screen.findByRole("combobox", { name: "Plant" })).toHaveValue("ironwood");
    expect(await screen.findByText("No tools at this plant.")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /D-01/ })).not.toBeInTheDocument();

    localStorage.setItem(SITE_STORAGE_KEY, "all");
    window.dispatchEvent(new StorageEvent("storage", { key: SITE_STORAGE_KEY, newValue: "all" }));
    expect(await screen.findByRole("link", { name: /D-01/ })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Plant" })).toHaveValue("all");
  });

  it("filters alerts, tickets, the open tool, investigations, and corrective actions", async () => {
    const user = userEvent.setup();
    server.use(
      http.get("/api/alerts", () => HttpResponse.json([northwaterAlert, ironwoodAlert])),
      http.get("/api/tickets", () =>
        HttpResponse.json([
          {
            id: "t-nw",
            number: "TKT-2001",
            incidentId: "i-nw",
            toolId: "E-02",
            title: "Northwater etch",
            openedAt: "2026-10-14T15:00:00Z",
          },
          {
            id: "t-iw",
            number: "TKT-2002",
            incidentId: "i-iw",
            toolId: "I-E-01",
            title: "Ironwood etch",
            openedAt: "2026-10-14T15:01:00Z",
          },
        ]),
      ),
    );

    renderConsole("/alerts");
    expect(await screen.findByRole("link", { name: "E-01" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "I-E-01" })).toBeInTheDocument();
    await user.selectOptions(screen.getByRole("combobox", { name: "Plant" }), "ironwood");
    expect(screen.queryByRole("link", { name: "E-01" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "I-E-01" })).toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "Tickets" }));
    expect(await screen.findByText("TKT-2002")).toBeInTheDocument();
    expect(screen.queryByText("TKT-2001")).not.toBeInTheDocument();

    await user.click(screen.getByRole("link", { name: "Tool" }));
    expect(await screen.findByText(/another plant/)).toBeInTheDocument();
  });

  it("hides an investigation that belongs to another plant", async () => {
    const user = userEvent.setup();
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    expect((await screen.findAllByRole("table", { name: "Evidence pack" })).length).toBeGreaterThan(0);
    await user.selectOptions(screen.getByRole("combobox", { name: "Plant" }), "lumen");
    expect(await screen.findByText(/another plant/)).toBeInTheDocument();
    expect(screen.queryByRole("table", { name: "Evidence pack" })).not.toBeInTheDocument();
  });

  it("hides a corrective action that belongs to another plant", async () => {
    const user = userEvent.setup();
    sessionStorage.setItem(
      "mps.capas",
      JSON.stringify([
        {
          id: "66666666-6666-4666-8666-666666666666",
          incidentId: S1_INCIDENT_ID,
          summary: "Hold the affected lots.",
          approvalTokenId: "77777777-7777-4777-8777-777777777777",
          actionHash: "ABC123",
          stage: "open",
          openedAt: "2026-10-14T15:05:00Z",
          approvedBy: "Bryan Weis",
          approvedAt: "2026-10-15T02:42:00.000Z",
        },
      ]),
    );
    renderConsole("/capas");
    expect(await screen.findByText("Hold the affected lots.")).toBeInTheDocument();
    await user.selectOptions(screen.getByRole("combobox", { name: "Plant" }), "ironwood");
    expect(await screen.findByText(/another plant/)).toBeInTheDocument();
    expect(screen.queryByText("Hold the affected lots.")).not.toBeInTheDocument();
  });
});
