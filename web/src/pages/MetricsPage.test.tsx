// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("metrics", () => {
  it("shows SPEC gates and leaves unreported scores blank", async () => {
    renderConsole("/metrics");
    expect(await screen.findByRole("heading", { name: "Metrics / eval" })).toBeInTheDocument();
    expect(screen.getAllByText("100% hard gate").length).toBeGreaterThan(0);
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
    expect(screen.getByText("n/a")).toBeInTheDocument();
    expect(screen.getByText(/illustrative layout only/i)).toBeInTheDocument();
  });
});
