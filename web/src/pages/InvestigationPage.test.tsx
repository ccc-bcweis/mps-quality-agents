// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";

function step(list: HTMLElement, title: string) {
  const item = within(list).getAllByRole("listitem").find((node) => node.textContent?.includes(title));
  if (!item) throw new Error(`Missing agent step ${title}`);
  return item;
}

describe("investigation", () => {
  it("shows the S1 evidence pack, the cited cause, and the confounder", async () => {
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    expect(await screen.findByText("ECO-4471 edge ring rev C")).toBeInTheDocument();
    expect(screen.getAllByText("Edge ring revision C changed the thermal path.").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/A preventive maintenance was performed the same week/).length).toBeGreaterThan(0);
    expect(screen.getByText("Rules")).toBeInTheDocument();
    expect(screen.getAllByText("AI").length).toBeGreaterThan(0);
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Recommendation");
    const steps = screen.getByRole("list", { name: "Agent steps" });
    expect(within(steps).getByText(/engineering change, equipment calibration, material lot, and process drift/)).toBeInTheDocument();
    expect(steps.querySelector('[data-status="running"]')).toBeNull();
    expect(step(steps, "Human approval")).toHaveAttribute("data-status", "queued");
    expect(screen.getByText("Why it matters: The part change was in place before the wobble started.")).toBeInTheDocument();
    const matrix = screen.getByRole("table", { name: "Root-cause evidence matrix" });
    expect(matrix).toHaveTextContent("supports");
    expect(matrix).toHaveTextContent("rules out");
    expect(screen.getAllByText(/A slow ramp would walk the mean/).length).toBeGreaterThan(0);
  });

  it("escalates S2 and does not assert a root cause", async () => {
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    expect((await screen.findAllByText(/No root cause is asserted/)).length).toBeGreaterThan(0);
    expect(screen.getAllByText(matchCopy(/PLM says showerhead revision C/)).length).toBeGreaterThan(0);
    expect(screen.queryByText("Edge ring revision C changed the thermal path.")).not.toBeInTheDocument();
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Investigation (escalated)");
    const steps = screen.getByRole("list", { name: "Agent steps" });
    expect(step(steps, "Evidence gate")).toHaveAttribute("data-status", "escalated");
    expect(step(steps, "Hypothesis ranking")).toHaveAttribute("data-status", "queued");
    expect(screen.getByText("Why it matters: MES says a different revision, so the cause cannot be named.")).toBeInTheDocument();
    expect(screen.getByRole("table", { name: "Root-cause evidence matrix" })).toHaveTextContent("rules out");
  });
});
