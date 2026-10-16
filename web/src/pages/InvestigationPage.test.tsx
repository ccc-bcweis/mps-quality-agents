// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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
    const pack = await screen.findByRole("table", { name: "Evidence pack" });
    expect(pack).toHaveTextContent("Product records");
    expect(pack).toHaveTextContent("The part change was in place before the wobble started.");
    expect(pack).toHaveTextContent("Etch tool E-02, Chamber B");
    expect(pack).toHaveTextContent("53.1 °C, recipe spec 48-56 °C, control limits 48-56 °C");
    expect(pack).toHaveTextContent("In spec, out of control");
    expect(screen.getAllByText("Edge ring revision C changed the thermal path.").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/A preventive maintenance was performed the same week/).length).toBeGreaterThan(0);
    expect(screen.getAllByText("Rules").length).toBeGreaterThan(0);
    expect(screen.getAllByText("AI").length).toBeGreaterThan(0);
    const steps = screen.getByRole("list", { name: "Agent steps" });
    expect(steps.textContent).toMatch(/engineering change, equipment calibration, material lot, and process drift/);
    expect(within(steps).getAllByText(/Duration/).length).toBeGreaterThan(0);
    expect(steps.querySelector('[data-status="running"]')).toBeNull();
    expect(step(steps, "Human approval")).toHaveAttribute("data-status", "queued");
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
    const steps = screen.getByRole("list", { name: "Agent steps" });
    expect(step(steps, "Evidence gate")).toHaveAttribute("data-status", "escalated");
    expect(step(steps, "Hypothesis ranking")).toHaveAttribute("data-status", "queued");
    expect(screen.getByRole("table", { name: "Evidence pack" })).toHaveTextContent(
      "MES says a different revision, so the cause cannot be named.",
    );
    expect(screen.getByRole("table", { name: "Evidence pack" })).toHaveTextContent("6.52 Torr, recipe spec 2-6 Torr");
    expect(screen.getByRole("table", { name: "Root-cause evidence matrix" })).toHaveTextContent("rules out");
  });

  it("puts the decision on the investigation page, without architecture remarks or the enforcement table", async () => {
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    const decision = await screen.findByRole("region", { name: "Decision" });
    const recommended = within(decision).getByTestId("recommended-action");
    expect(recommended).toHaveTextContent("Recommended action");
    expect(recommended).toHaveTextContent(/Containment:.*roll back to edge ring revision B/);
    expect(recommended).toHaveTextContent(/Preventive:.*ECO-4471/);
    expect(recommended.compareDocumentPosition(within(decision).getByRole("button", { name: "Reject" })) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(decision).getByRole("button", { name: "Approve CAPA + containment" })).toBeInTheDocument();
    expect(within(decision).getByRole("button", { name: "Request more evidence" })).toBeInTheDocument();
    expect(within(decision).getByRole("button", { name: "Reject" })).toBeInTheDocument();
    expect(within(decision).getByLabelText("Note or justification")).toBeInTheDocument();
    expect(within(decision).getByLabelText("Approver name")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Go to decision" })).toHaveAttribute("href", "#decision");
    expect(document.body.textContent).not.toMatch(/write authority|Enforcement|No approver name|human-gated/);
  });

  it("an old approval link opens the investigation at its decision", async () => {
    renderConsole(`/approvals/${S1_INVESTIGATION_ID}`);
    expect(await screen.findByRole("region", { name: "Decision" })).toBeInTheDocument();
    expect(screen.getByRole("table", { name: "Evidence pack" })).toBeInTheDocument();
  });

  it("agent steps open and close from the whole row with a chevron", async () => {
    const user = userEvent.setup();
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    const steps = await screen.findByRole("list", { name: "Agent steps" });
    expect(within(steps).queryByText("Details")).not.toBeInTheDocument();
    const row = within(steps).getAllByRole("button")[0];
    expect(row).toHaveAttribute("aria-expanded", "false");
    await user.click(row);
    expect(row).toHaveAttribute("aria-expanded", "true");
    await user.click(row);
    expect(row).toHaveAttribute("aria-expanded", "false");
  });
});
