// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";
import { matchCopy } from "../test/matchCopy";

describe("fleet overview", () => {
  it("shows the eight-tool fab with alarm and watch tiles", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    expect(await screen.findByRole("heading", { name: "Fleet overview" })).toBeInTheDocument();
    for (const id of ["D-01", "D-02", "E-01", "E-02", "S-01", "S-02", "C-01", "C-02"]) {
      expect(screen.getByRole("link", { name: new RegExp(id) })).toBeInTheDocument();
    }
    const alarm = screen.getByRole("link", { name: /D-01/ });
    expect(alarm.textContent).toMatch(/ALARM/);
    expect(alarm.textContent).not.toMatch(/wafers\/h/);
    expect(screen.getByRole("link", { name: /E-02/ }).textContent).toMatch(/WATCH/);
    expect(screen.getByRole("img", { name: "Meridian Precision Systems mark" })).toBeInTheDocument();
    expect(screen.getByText("Powered by StatusNeo")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Fleet performance" })).toHaveTextContent("Illustrative demo readings, not a live tool counter.");
    expect(screen.getByText("26.4 wafers/h")).toBeInTheDocument();
    expect(screen.getByText("78.4%")).toBeInTheDocument();
    expect(screen.getByText("22.0 wafers/h")).toBeInTheDocument();
    const oee = screen.getByText(matchCopy(/overall equipment effectiveness \(OEE\)/));
    await user.click(oee);
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/good-wafer rate/i);
  });
});
