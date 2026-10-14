// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";

describe("alerts", () => {
  it("lists the rules-engine rows and the linked ticket", async () => {
    renderConsole("/alerts");
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/Western Electric rule 1 \(WE-1\) · 1 point > 3σ/))).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/Western Electric rule 2 \(WE-2\) · 2 of 3 > 2σ/))).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "TKT-1044" })).toBeInTheDocument();
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Alert");
  });
});
