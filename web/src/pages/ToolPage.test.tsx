// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";
import { matchCopy } from "../test/matchCopy";

describe("tool detail", () => {
  it("draws an SPC chart and the etch windows for E-02", async () => {
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("heading", { name: /Tool E-02/ })).toBeInTheDocument();
    const chart = await screen.findByRole("figure", { name: /SPC chart of ESC temperature/ });
    expect(chart).toHaveAccessibleName(/Western Electric rule 2/i);
    expect(screen.getByRole("cell", { name: "40–60 °C" })).toBeInTheDocument();
    expect(screen.getAllByText("53.1 °C osc").length).toBeGreaterThan(0);
    expect(screen.getByRole("region", { name: "Tool performance" })).toHaveTextContent("24.0 wafers/h");
    expect(screen.getByText("Illustrative demo readings, not a live tool counter.")).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/mean time between failures \(MTBF\)/))).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/mean time to repair \(MTTR\)/))).toBeInTheDocument();
  });

  it("binds etch sensors to the schematic and opens the chart for the point that was chosen", async () => {
    const user = userEvent.setup();
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("region", { name: /plasma etch schematic/i })).toBeInTheDocument();
    const esc = screen.getByRole("button", { name: /E-02-A-TC-ESC/ });
    expect(esc).toHaveTextContent("53.1");
    expect(esc).toHaveTextContent("C");
    expect(esc).toHaveAttribute("data-status", "inSpec");
    expect(esc).toHaveAttribute("data-anchor", "a.chuck.esc");
    expect(screen.getByRole("button", { name: /E-02-A-OUT-ENDPOINT/ })).toHaveAttribute("data-anchor", "a.chamber.endpoint");
    expect(screen.queryByText(/is not on the drawing/)).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /E-02-A-PT-CHAMBER/ }));
    expect(await screen.findByRole("figure", { name: /SPC chart of Chamber pressure/ })).toBeInTheDocument();
  });

  it("marks deposition chamber pressure out of spec", async () => {
    renderConsole("/tools/D-01");
    const pressure = await screen.findByRole("button", { name: /D-01-A-PT-CHAMBER/ });
    expect(pressure).toHaveTextContent("6.52");
    expect(pressure).toHaveTextContent("Torr");
    expect(pressure).toHaveAttribute("data-status", "outOfSpec");
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
});
