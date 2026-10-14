// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";
import { renderConsole } from "../test/render";

describe("investigation", () => {
  it("shows the S1 evidence pack, the cited cause, and the confounder", async () => {
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    expect(await screen.findByText("ECO-4471 edge ring rev C")).toBeInTheDocument();
    expect(screen.getAllByText("Edge ring revision C changed the thermal path.").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/A preventive maintenance was performed the same week/).length).toBeGreaterThan(0);
    expect(screen.getByText("Rules")).toBeInTheDocument();
    expect(screen.getAllByText("AI").length).toBeGreaterThan(0);
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Recommendation");
  });

  it("escalates S2 and does not assert a root cause", async () => {
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    expect((await screen.findAllByText(/No root cause is asserted/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/PLM says showerhead revision C/).length).toBeGreaterThan(0);
    expect(screen.queryByText("Edge ring revision C changed the thermal path.")).not.toBeInTheDocument();
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Investigation (escalated)");
  });
});
