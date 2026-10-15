// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { HARNESS_STORAGE_KEY } from "./HarnessBar";
import { server } from "../test/server";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

const DATA_TIP = "Live follows the running plant. Replay shows a recorded scenario. This does not change how the AI answers.";
const SOFT_COPY =
  "Soft reset closes open tickets and investigations as Closed by demo reset. It deletes nothing. History stays, and every tool returns to nominal.";

const baseline = {
  seed: 4242,
  simulatedOrigin: "2026-09-01T00:00:00Z",
  fleetToolCount: 8,
  ticketCount: 0,
  investigationCount: 0,
  approvalCount: 0,
  capaCount: 0,
  telemetryCount: 12000,
  eventCount: 1,
  resetEventId: "reset-event",
  kind: "hard" as const,
};

describe("harness collapse, replay, and soft reset", () => {
  it("starts collapsed and remembers the open row in this browser", async () => {
    const user = userEvent.setup();
    const first = renderConsole("/");
    const harness = await screen.findByRole("banner", { name: "StatusNeo demo harness" });
    const expand = within(harness).getByRole("button", { name: "Expand harness" });
    expect(expand).toHaveAttribute("aria-expanded", "false");
    expect(expand).toHaveTextContent("▼");
    expect(within(harness).queryByRole("combobox", { name: "Scenario" })).not.toBeInTheDocument();
    expect(harness.className).not.toMatch(/truncate|overflow-hidden/);
    await waitFor(() => {
      expect(within(harness).getByRole("status", { name: "Harness status" })).toHaveTextContent("Live · SC-1 · Saved answers");
    });
    await user.click(expand);
    expect(localStorage.getItem(HARNESS_STORAGE_KEY)).toBe("expanded");
    expect(within(harness).getByRole("button", { name: "Collapse harness" })).toHaveAttribute("aria-expanded", "true");
    expect(within(harness).getByRole("combobox", { name: "Scenario" })).toBeInTheDocument();
    const controls = harness.querySelector("[data-region='harness-controls']");
    expect(controls?.className).not.toMatch(/overflow-(y-)?auto|max-h-/);
    expect(controls?.className).toContain("lg:grid-cols-2");
    first.unmount();
    renderConsole("/");
    const again = await screen.findByRole("button", { name: "Collapse harness" });
    expect(again).toHaveAttribute("aria-expanded", "true");
    await user.click(again);
    expect(localStorage.getItem(HARNESS_STORAGE_KEY)).toBe("collapsed");
    expect(screen.queryByRole("combobox", { name: "Scenario" })).not.toBeInTheDocument();
  });

  it("swaps the console to a recorded scenario and restores live", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const harness = await expandHarness();
    await within(harness).findByRole("option", { name: /SC-4/ });
    await user.selectOptions(within(harness).getByRole("combobox", { name: "Scenario" }), "SC-4");
    const plant = within(harness).getByRole("group", { name: "Plant data" });
    expect(plant).toHaveAttribute("title", DATA_TIP);
    expect(within(plant).getByRole("button", { name: "Replay" })).toHaveAttribute("title", expect.stringMatching(/recorded messages/));
    await user.click(within(plant).getByRole("button", { name: "Replay" }));
    const banner = await screen.findByRole("status", { name: "Replay banner" });
    expect(banner).toHaveTextContent("recorded run for SC-4");
    const consoleRegion = screen.getByRole("region", { name: "Meridian Precision Systems console" });
    expect(consoleRegion).toContainElement(banner);
    expect(harness.contains(banner)).toBe(false);
    expect(banner.className).toContain("shrink-0");
    expect(banner.compareDocumentPosition(within(consoleRegion).getByRole("main")) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(harness).getByRole("status", { name: "Harness status" })).toHaveTextContent("Replay · SC-4 · Saved answers");
    await user.click(within(plant).getByRole("button", { name: "Live" }));
    await waitFor(() => {
      expect(screen.queryByRole("status", { name: "Replay banner" })).not.toBeInTheDocument();
    });
    expect(within(harness).getByRole("status", { name: "Harness status" })).toHaveTextContent("Live · SC-4 · Saved answers");
  });

  it("closes open work on a soft reset and keeps the page", async () => {
    const user = userEvent.setup();
    renderConsole("/tickets");
    const harness = await expandHarness();
    await user.click(within(harness).getByRole("button", { name: "Soft reset" }));
    expect(within(harness).getByRole("button", { name: "Soft reset" })).toHaveAttribute("title", expect.stringMatching(/Deletes nothing/));
    expect(screen.getByRole("dialog", { name: SOFT_COPY })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Confirm soft reset" }));
    expect(await screen.findByText("Open work closed. Seed 4242. History was kept.")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Tickets" })).toBeInTheDocument();
  });

  it("shows reset progress in the harness while the console stays up", async () => {
    let release: () => void = () => {};
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get("/api/admin/reset/progress", () =>
        HttpResponse.json({
          running: true,
          kind: "hard",
          phase: "seeding",
          message: "Filling a week of healthy readings.",
        }),
      ),
      http.post("/api/admin/reset", async () => {
        await gate;
        return HttpResponse.json(baseline);
      }),
    );
    const user = userEvent.setup();
    renderConsole("/");
    const harness = await expandHarness();
    await user.click(within(harness).getByRole("button", { name: "Hard reset" }));
    await user.click(screen.getByRole("button", { name: "Confirm hard reset" }));
    expect(await screen.findByText("Filling a week of healthy readings.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Fleet overview" })).toBeInTheDocument();
    release();
    expect(await screen.findByText("Demo baseline restored. Seed 4242.")).toBeInTheDocument();
  });

  it("keeps the chosen scenario and shows the running one across tab changes and a remount", async () => {
    const user = userEvent.setup();
    const first = renderConsole("/alerts");
    let harness = await expandHarness();
    await within(harness).findByRole("option", { name: /SC-4/ });
    await user.selectOptions(within(harness).getByRole("combobox", { name: "Scenario" }), "SC-4");
    await user.click(screen.getByRole("link", { name: "Tickets" }));
    await screen.findByRole("heading", { name: "Tickets" });
    expect(screen.getByRole("combobox", { name: "Scenario" })).toHaveValue("SC-4");
    await user.click(screen.getByRole("button", { name: "Run scenario" }));
    await waitFor(() => expect(screen.getByRole("status", { name: "Harness status" })).toHaveTextContent("Running SC-4"));
    first.unmount();

    renderConsole("/");
    harness = await expandHarness();
    await within(harness).findByRole("option", { name: /SC-4/ });
    expect(within(harness).getByRole("combobox", { name: "Scenario" })).toHaveValue("SC-4");
    expect(screen.getByRole("status", { name: "Harness status" })).toHaveTextContent("Running SC-4");
  });

  it("offers a data speed and sample interval", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const harness = await expandHarness();
    const speed = within(harness).getByRole("combobox", { name: "Speed" });
    expect(within(speed).getAllByRole("option").map((option) => option.textContent)).toEqual(["1x", "2x", "5x", "10x"]);
    if (!(speed as HTMLSelectElement).disabled) {
      await user.selectOptions(speed, "5");
      await waitFor(() => expect(speed).toHaveValue("5"));
    }
    expect(within(harness).getByRole("combobox", { name: "Sample every" })).toBeInTheDocument();
  });
});
