// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
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

  it("says when a tool id is unknown", async () => {
    renderConsole("/tools/Z-99");
    expect(await screen.findByText("Tool was not found.")).toBeInTheDocument();
  });
});
