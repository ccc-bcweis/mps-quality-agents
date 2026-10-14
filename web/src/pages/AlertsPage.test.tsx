// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("alerts", () => {
  it("lists the rules-engine rows and the linked ticket", async () => {
    renderConsole("/alerts");
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.getByText("WE-1 · 1 point > 3σ")).toBeInTheDocument();
    expect(screen.getByText("WE-2 · 2 of 3 > 2σ")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "TKT-1044" })).toBeInTheDocument();
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Alert");
  });
});
