// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("roi", () => {
  it("labels the PLAN figures as illustrative", async () => {
    renderConsole("/roi");
    expect(await screen.findByRole("heading", { name: /ROI/ })).toBeInTheDocument();
    expect(screen.getByText(/Illustrative, synthetic/)).toBeInTheDocument();
    expect(screen.getByText("$96M")).toBeInTheDocument();
    expect(screen.getByText(/\$7,599,195/)).toBeInTheDocument();
  });
});
