// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";

describe("alerts", () => {
  it("starts with an empty list", async () => {
    renderConsole("/alerts");
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(screen.queryByText(matchCopy(/Western Electric rules \(WE-1\) · 1 point > 3σ/))).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "TKT-1044" })).not.toBeInTheDocument();
  });
});
