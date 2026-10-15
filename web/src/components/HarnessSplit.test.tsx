// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { CONSOLE_TABS } from "../shell/navigation";
import { renderConsole } from "../test/render";

const CONSOLE_TAB_LABELS = ["Sites", "Fleet", "Tool", "Alerts", "Tickets", "Investigation", "Approval", "CAPA"];

describe("demo harness and console split", () => {
  it("keeps presenter controls out of the console region", async () => {
    renderConsole("/");
    const harness = await screen.findByRole("banner", { name: "StatusNeo demo harness" });
    const consoleRegion = screen.getByRole("region", { name: "Meridian Precision Systems console" });
    expect(consoleRegion.contains(harness)).toBe(false);
    expect(harness.contains(consoleRegion)).toBe(false);
    expect(harness.className).toContain("bg-sn-navy");
    expect(harness.className).not.toMatch(/truncate|overflow-hidden/);
    expect(consoleRegion.className).toContain("bg-mps-canvas");
    expect(consoleRegion.className).toContain("overflow-hidden");
    expect(consoleRegion.className).not.toMatch(/border-sn-gold|border-amber|border-yellow/);
    const scrolling = within(consoleRegion).getByRole("main");
    expect(scrolling).toHaveAttribute("data-scroll", "content");
    expect(scrolling.className).toContain("overflow-y-auto");
    expect(consoleRegion.querySelector("header")?.className).toContain("shrink-0");
    expect(consoleRegion.querySelector("footer")?.className).toContain("shrink-0");

    expect(CONSOLE_TABS.map((tab) => tab.label)).toEqual(CONSOLE_TAB_LABELS);
    const tabs = within(consoleRegion).getByRole("navigation", { name: "Console" });
    expect(tabs).toHaveAttribute("data-sites-slot", "before-fleet");
    expect(within(tabs).getAllByRole("link").map((link) => link.textContent?.replace(/\s+/g, " ").trim())).toEqual(CONSOLE_TAB_LABELS);

    expect(within(harness).getByRole("link", { name: "Fault injector" })).toHaveAttribute("href", "/harness/injector");
    expect(within(harness).getByRole("link", { name: "Return on investment" })).toHaveAttribute("href", "/harness/roi");
    expect(within(harness).getByRole("link", { name: "Metrics" })).toHaveAttribute("href", "/harness/metrics");
    expect(within(harness).getByRole("button", { name: "Reset demo" })).toBeInTheDocument();
    expect(within(harness).getByRole("combobox", { name: "Scenario" })).toBeInTheDocument();
    expect(within(harness).getByText("REPLAY MODE")).toBeInTheDocument();
    expect(within(harness).getByText("$0.00 of $2.00")).toBeInTheDocument();

    expect(within(consoleRegion).queryByRole("link", { name: "Fault injector" })).not.toBeInTheDocument();
    expect(within(consoleRegion).queryByRole("link", { name: "Return on investment" })).not.toBeInTheDocument();
    expect(within(consoleRegion).queryByRole("link", { name: "Metrics" })).not.toBeInTheDocument();
    expect(within(consoleRegion).queryByRole("button", { name: "Reset demo" })).not.toBeInTheDocument();
    expect(within(consoleRegion).queryByRole("combobox", { name: "Scenario" })).not.toBeInTheDocument();
    expect(within(consoleRegion).queryByText("REPLAY MODE")).not.toBeInTheDocument();
    expect(within(tabs).queryByRole("link", { name: "Glossary" })).not.toBeInTheDocument();
    expect(within(consoleRegion).getByRole("link", { name: "Glossary" })).toHaveAttribute("href", "/glossary");
  });

  it("opens the fault injector as a harness panel, not a console tab", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    await user.click(await screen.findByRole("link", { name: "Fault injector" }));
    const panel = await screen.findByRole("region", { name: "Demo harness panel" });
    expect(within(panel).getByRole("heading", { name: "Fault injector" })).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Meridian Precision Systems console" })).not.toBeInTheDocument();
    expect(within(panel).queryByRole("navigation", { name: "Console" })).not.toBeInTheDocument();
    await user.click(within(panel).getByRole("link", { name: "Open the console" }));
    expect(await screen.findByRole("heading", { name: "Fleet overview" })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Meridian Precision Systems console" })).toBeInTheDocument();
  });

  it("keeps the old presenter paths on the harness", async () => {
    renderConsole("/roi");
    const panel = await screen.findByRole("region", { name: "Demo harness panel" });
    expect(within(panel).getByRole("heading", { name: /ROI/ })).toBeInTheDocument();
    expect(screen.queryByRole("navigation", { name: "Console" })).not.toBeInTheDocument();
  });

  it("runs a named scenario from the harness bar", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const harness = await screen.findByRole("banner", { name: "StatusNeo demo harness" });
    await within(harness).findByRole("option", { name: /SC-4/ });
    await user.selectOptions(within(harness).getByRole("combobox", { name: "Scenario" }), "SC-4");
    await user.click(within(harness).getByRole("button", { name: "Run scenario" }));
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    const consoleRegion = screen.getByRole("region", { name: "Meridian Precision Systems console" });
    expect(consoleRegion).toContainElement(screen.getByRole("heading", { name: "Alerts" }));
    expect(await within(consoleRegion).findByRole("link", { name: "E-01" })).toBeInTheDocument();
    expect(within(consoleRegion).queryByText("corr-scenario-SC-4")).not.toBeInTheDocument();
  });
});
