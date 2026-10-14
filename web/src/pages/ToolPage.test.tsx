// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("tool detail", () => {
  it("draws an SPC chart and the etch windows for E-02", async () => {
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("heading", { name: /Tool E-02/ })).toBeInTheDocument();
    const chart = await screen.findByRole("figure", { name: /SPC chart of ESC temperature/ });
    expect(chart).toHaveAccessibleName(/Western Electric rule 2/i);
    expect(screen.getByRole("cell", { name: "40–60 °C" })).toBeInTheDocument();
    expect(screen.getAllByText("53.1 °C osc").length).toBeGreaterThan(0);
  });

  it("binds etch sensors to the schematic and opens the chart for the point that was chosen", async () => {
    const user = userEvent.setup();
    renderConsole("/tools/E-02");
    expect(await screen.findByRole("img", { name: /plasma etch schematic/i })).toBeInTheDocument();
    const esc = screen.getByRole("button", { name: /ESC temperature/i });
    expect(esc).toHaveTextContent("53.1 °C osc");
    expect(esc).toHaveAttribute("data-status", "warning");
    expect(screen.getByText(/Endpoint time \(endpoint\.time\) is not on the drawing/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Chamber pressure/i }));
    expect(await screen.findByRole("figure", { name: /SPC chart of Chamber pressure/ })).toBeInTheDocument();
  });

  it("marks deposition chamber pressure out of spec", async () => {
    renderConsole("/tools/D-01");
    const pressure = await screen.findByRole("button", { name: /Chamber pressure/i });
    expect(pressure).toHaveTextContent("6.52 Torr");
    expect(pressure).toHaveAttribute("data-status", "outOfSpec");
    expect(screen.getByRole("img", { name: /thin-film deposition schematic/i })).toBeInTheDocument();
  });

  it("draws the strip and clean schematics", async () => {
    renderConsole("/tools/S-01");
    expect(await screen.findByRole("img", { name: /photoresist strip schematic/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Remote plasma/i })).toHaveTextContent("3.1 kW");
    renderConsole("/tools/C-01");
    expect(await screen.findByRole("img", { name: /single-wafer clean schematic/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Megasonic/i })).toHaveTextContent("720 W");
  });

  it("says when a tool id is unknown", async () => {
    renderConsole("/tools/Z-99");
    expect(await screen.findByText("Tool was not found.")).toBeInTheDocument();
  });
});
