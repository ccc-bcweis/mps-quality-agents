// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("fleet overview", () => {
  it("shows the eight-tool fab with alarm and watch tiles", async () => {
    renderConsole("/");
    expect(await screen.findByRole("heading", { name: "Fleet overview" })).toBeInTheDocument();
    for (const id of ["D-01", "D-02", "E-01", "E-02", "S-01", "S-02", "C-01", "C-02"]) {
      expect(screen.getByRole("link", { name: new RegExp(id) })).toBeInTheDocument();
    }
    expect(screen.getByRole("link", { name: /D-01/ }).textContent).toMatch(/ALARM/);
    expect(screen.getByRole("link", { name: /E-02/ }).textContent).toMatch(/WATCH/);
    expect(screen.getByRole("img", { name: "Meridian Precision Systems mark" })).toBeInTheDocument();
    expect(screen.getByText("Powered by StatusNeo")).toBeInTheDocument();
  });
});
