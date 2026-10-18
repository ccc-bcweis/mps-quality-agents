// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { emitHubEvent } from "../live/fleetHub";
import { mockHarness } from "../mocks/state";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

const IDLE = /^Live · Offline \(saved answers\)$/;

async function harnessControls() {
  const harness = await expandHarness();
  return {
    harness,
    status: within(harness).getByRole("status", { name: "Harness status" }),
    scenario: within(harness).getByRole("combobox", { name: "Scenario" }),
    speed: within(harness).getByRole("combobox", { name: "Speed" }),
    stride: within(harness).getByRole("combobox", { name: "Sample every" }),
  };
}

// Live (Oct 17): after a hard reset with nothing running the banner said "Live · SC-8" (the picker's choice) or
// "Running SC-2" (kept in the tab), and the speed said 10x / 5 s although the server was back at 1x / 15 s.
// The banner, picker and speed now follow the server, in every tab.
describe("harness state follows the server", () => {
  it("shows only Live and the engine when nothing runs, with no scenario picked and the server's speed", async () => {
    mockHarness.speed = 5;
    mockHarness.strideSeconds = 30;
    renderConsole("/");
    const { status, scenario, speed, stride } = await harnessControls();
    await waitFor(() => expect(speed).toHaveValue("5"));
    expect(stride).toHaveValue("30");
    await waitFor(() => expect(status).toHaveTextContent(IDLE));
    expect(scenario).toHaveValue("");
    expect(within(scenario).getByRole("option", { name: "Choose a scenario" })).toBeInTheDocument();
  });

  it("says Running only while the server runs a scenario: run, then Stop scenario", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const controls = await harnessControls();
    await within(controls.harness).findByRole("option", { name: /SC-4/ });
    await user.selectOptions(controls.scenario, "SC-4");
    await user.click(within(controls.harness).getByRole("button", { name: "Run scenario" }));
    await waitFor(() => expect(screen.getByRole("status", { name: "Harness status" })).toHaveTextContent("Live · Running SC-4 · Offline (saved answers)"));

    // Running a scenario opens its tool page; read the harness again there.
    await waitFor(() => expect(screen.getAllByRole("heading", { level: 1 }).some((heading) => /^Tool /.test(heading.textContent ?? ""))).toBe(true));
    const stopButton = await screen.findByRole("button", { name: "Stop scenario" });
    await user.click(stopButton);
    await waitFor(() => expect(screen.getByRole("status", { name: "Harness status" })).toHaveTextContent(IDLE));
    expect(mockHarness.runningScenarioId).toBeNull();
  });

  it("returns to not running, no pick and 1x / 15 s after a hard reset in this tab", async () => {
    const user = userEvent.setup();
    mockHarness.runningScenarioId = "SC-8";
    mockHarness.speed = 10;
    mockHarness.strideSeconds = 5;
    renderConsole("/");
    let controls = await harnessControls();
    await waitFor(() => expect(controls.status).toHaveTextContent("Running SC-8"));
    await within(controls.harness).findByRole("option", { name: /SC-8/ });
    await user.selectOptions(controls.scenario, "SC-8");

    await user.click(within(controls.harness).getByRole("button", { name: "Hard reset" }));
    await user.click(screen.getByRole("button", { name: "Confirm hard reset" }));
    expect(await screen.findByText("Demo baseline restored. Seed 4242.")).toBeInTheDocument();

    controls = await harnessControls();
    await waitFor(() => expect(controls.status).toHaveTextContent(IDLE));
    expect(controls.scenario).toHaveValue("");
    await waitFor(() => expect(controls.speed).toHaveValue("1"));
    expect(controls.stride).toHaveValue("15");
  });

  it("follows a reset from another browser (resync) and a containment (clock with no scenario)", async () => {
    const user = userEvent.setup();
    mockHarness.runningScenarioId = "SC-2";
    mockHarness.speed = 10;
    mockHarness.strideSeconds = 5;
    renderConsole("/");
    const controls = await harnessControls();
    await waitFor(() => expect(controls.status).toHaveTextContent("Running SC-2"));
    await within(controls.harness).findByRole("option", { name: /SC-2/ });
    await user.selectOptions(controls.scenario, "SC-2");

    // Another browser resets: the server is back at 1x / 15 s with nothing running, then every page resyncs.
    mockHarness.runningScenarioId = null;
    mockHarness.speed = 1;
    mockHarness.strideSeconds = 15;
    act(() => emitHubEvent("resync", { reason: "reset" }));
    await waitFor(() => expect(controls.status).toHaveTextContent(IDLE));
    expect(controls.scenario).toHaveValue("");
    await waitFor(() => expect(controls.speed).toHaveValue("1"));
    expect(controls.stride).toHaveValue("15");

    // The clock message carries the server state: a scenario started elsewhere, then a CAPA moved to containment.
    act(() => emitHubEvent("clock", { simulatedNow: "2026-10-17T23:30:00Z", speed: 5, strideSeconds: 30, runningScenarioId: "SC-7" }));
    await waitFor(() => expect(controls.status).toHaveTextContent("Running SC-7"));
    expect(controls.speed).toHaveValue("5");
    expect(controls.stride).toHaveValue("30");
    act(() => emitHubEvent("clock", { simulatedNow: "2026-10-17T23:30:05Z", speed: 5, strideSeconds: 30, runningScenarioId: null }));
    await waitFor(() => expect(controls.status).toHaveTextContent(IDLE));
  });
});
