// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { EvidenceItem, Hypothesis, Investigation } from "../api/types";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID, investigationFor } from "../mocks/demoData";
import { server } from "../test/server";
import { renderConsole } from "../test/render";

function follows(before: Element, after: Element): boolean {
  return (before.compareDocumentPosition(after) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}

function load(id: string, body: Investigation) {
  server.use(http.get(`/api/investigations/${id}`, () => HttpResponse.json(body)));
}

describe("ruled out records", () => {
  it("keeps supporting records first and lists ruled-out records with the reason and the source", async () => {
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    const pack = await screen.findByRole("table", { name: "Evidence pack" });
    const group = screen.getByRole("region", { name: "Considered and ruled out" });
    expect(follows(pack, group)).toBe(true);
    expect(pack).toHaveTextContent("Product records");
    expect(pack).not.toHaveTextContent("SN-SIH4-088");
    expect(pack).not.toHaveTextContent("Preventive maintenance the same week");
    const ruled = within(group).getAllByRole("listitem");
    expect(ruled).toHaveLength(2);
    expect(ruled[0]).toHaveTextContent("PM-3381: Preventive maintenance the same week");
    expect(ruled[0]).toHaveTextContent("The scheduled maintenance does not explain the oscillation.");
    expect(ruled[0]).toHaveTextContent("The AI ruled this out");
    expect(ruled[0]).not.toHaveTextContent("From the record");
    expect(ruled[1]).toHaveTextContent("SN-SIH4-088: Silane lot SIH4-088 supplier notice");
    expect(ruled[1]).toHaveTextContent(/silane lot SIH4-088 matches the purchase specification/i);
    expect(ruled[1]).toHaveTextContent("From the record");
    expect(ruled[1]).not.toHaveTextContent("The AI ruled this out");
    expect(screen.queryByText("Nothing ruled out")).not.toBeInTheDocument();

    const decision = screen.getByRole("region", { name: "Decision" });
    const against = within(decision).getByText("Evidence against this:");
    const list = within(decision).getByRole("list", { name: "Ruled out records" });
    expect(follows(against, list)).toBe(true);
    expect(list).toHaveTextContent("SN-SIH4-088");
    expect(list).toHaveTextContent("matches the purchase specification");
    expect(list).toHaveTextContent("Preventive maintenance the same week");
    expect(list).toHaveTextContent("does not explain the oscillation");
    expect(list).not.toHaveTextContent("From the record");
    expect(list).not.toHaveTextContent("The AI ruled this out");

    const hypotheses = screen.getByRole("heading", { name: "Hypotheses" }).closest("section");
    if (!hypotheses) throw new Error("Missing hypotheses");
    const top = within(hypotheses)
      .getAllByRole("listitem")
      .find((node) => (node.textContent ?? "").includes("Edge ring revision C changed the thermal path"));
    if (!top) throw new Error("Missing top hypothesis");
    expect(within(top).getByText("Evidence against this:")).toBeInTheDocument();
    expect(within(top).getByRole("list", { name: "Ruled out records" })).toHaveTextContent("SN-SIH4-088");
  });

  it("hides the evidence group and the decision list when nothing was ruled out", async () => {
    const base = investigationFor(S1_INVESTIGATION_ID);
    if (!base) throw new Error("Missing S1");
    load(S1_INVESTIGATION_ID, {
      ...base,
      evidence: base.evidence.map((item) => ({ ...item, status: "candidate" })),
      hypotheses: base.hypotheses.map((item) => ({ ...item, ruledOut: [] })),
    });
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    await screen.findByRole("table", { name: "Evidence pack" });
    expect(screen.queryByRole("region", { name: "Considered and ruled out" })).not.toBeInTheDocument();
    expect(screen.queryByText("Considered and ruled out")).not.toBeInTheDocument();
    expect(screen.queryByText("Nothing ruled out")).not.toBeInTheDocument();
    const decision = screen.getByRole("region", { name: "Decision" });
    expect(within(decision).queryByRole("list", { name: "Ruled out records" })).not.toBeInTheDocument();
    expect(within(decision).getByText("Evidence against this:")).toBeInTheDocument();
  });

  it("treats a missing field as empty and ignores a Ruled out title prefix", async () => {
    const base = investigationFor(S1_INVESTIGATION_ID);
    if (!base) throw new Error("Missing S1");
    const evidence: EvidenceItem[] = base.evidence.map((item) => {
      const copy: EvidenceItem = {
        ...item,
        title: item.externalRef === "SN-SIH4-088" ? "Ruled out: Silane lot SIH4-088 supplier notice" : item.title,
      };
      delete copy.status;
      return copy;
    });
    const hypotheses: Hypothesis[] = base.hypotheses.map((item) => {
      const copy: Hypothesis = { ...item };
      delete copy.ruledOut;
      return copy;
    });
    load(S1_INVESTIGATION_ID, { ...base, evidence, hypotheses });
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    const pack = await screen.findByRole("table", { name: "Evidence pack" });
    expect(pack).toHaveTextContent("Ruled out: Silane lot SIH4-088 supplier notice");
    expect(screen.queryByRole("region", { name: "Considered and ruled out" })).not.toBeInTheDocument();
    expect(screen.queryByText("Nothing ruled out")).not.toBeInTheDocument();
    const decision = screen.getByRole("region", { name: "Decision" });
    expect(within(decision).queryByRole("list", { name: "Ruled out records" })).not.toBeInTheDocument();
  });

  it("lists records the refused answer proposed as ruled out beside the cited records", async () => {
    const user = userEvent.setup();
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const detail = await screen.findByText("What the AI proposed (not accepted)");
    await user.click(detail);
    const box = detail.closest("details");
    if (!box) throw new Error("Missing rejected answer");
    const cited = within(box).getByText("Cited as support");
    const proposed = within(box).getByText("Proposed as ruled out");
    expect(follows(cited, proposed)).toBe(true);
    expect(box).toHaveTextContent("SN-SIH4-088: Silane lot SIH4-088 supplier notice. Silane lot SIH4-088 matches the purchase specification.");
    expect(box).toHaveTextContent("PM-D02-SCH: Scheduled pump maintenance. The scheduled maintenance does not explain the pressure step.");
    expect(screen.queryByText("Nothing ruled out")).not.toBeInTheDocument();
  });

  it("hides proposed as ruled out when that list is empty", async () => {
    const user = userEvent.setup();
    const base = investigationFor(S2_INVESTIGATION_ID);
    if (!base?.rejectedAnswer) throw new Error("Missing S2");
    load(S2_INVESTIGATION_ID, { ...base, rejectedAnswer: { ...base.rejectedAnswer, ruledOutIds: [] } });
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const detail = await screen.findByText("What the AI proposed (not accepted)");
    await user.click(detail);
    const box = detail.closest("details");
    if (!box) throw new Error("Missing rejected answer");
    expect(within(box).getByText("Cited as support")).toBeInTheDocument();
    expect(within(box).queryByText("Proposed as ruled out")).not.toBeInTheDocument();
    expect(screen.queryByText("Nothing ruled out")).not.toBeInTheDocument();
  });

  it("hides proposed as ruled out when an old answer omits ruledOutIds", async () => {
    const user = userEvent.setup();
    const base = investigationFor(S2_INVESTIGATION_ID);
    if (!base?.rejectedAnswer) throw new Error("Missing S2");
    const rejected = { ...base.rejectedAnswer };
    delete rejected.ruledOutIds;
    load(S2_INVESTIGATION_ID, { ...base, rejectedAnswer: rejected });
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const detail = await screen.findByText("What the AI proposed (not accepted)");
    await user.click(detail);
    const box = detail.closest("details");
    if (!box) throw new Error("Missing rejected answer");
    expect(within(box).getByText("Cited as support")).toBeInTheDocument();
    expect(within(box).queryByText("Proposed as ruled out")).not.toBeInTheDocument();
  });
});
