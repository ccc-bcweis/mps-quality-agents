// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import type { Investigation } from "../api/types";
import { AddEvidenceBox } from "../components/AddEvidenceBox";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID, investigationFor } from "../mocks/demoData";
import { server } from "../test/server";
import { renderConsole } from "../test/render";

const NOTE = "Checked on the floor: the turbo pump bearing on E-02 is worn and past its service interval.";

function signedIn() {
  server.use(http.get("/api/admin/session", () => HttpResponse.json({ mode: "local", isAdmin: true, displayName: "Bryan Weis" })));
}

describe("evidence loop", () => {
  it("Request more evidence opens Add evidence, needs text, and runs round 2 with the note marked as user-provided", async () => {
    signedIn();
    let sent: { kind?: string; comment?: string | null } | null = null;
    const listen = async ({ request }: { request: Request }) => {
      if (request.method === "POST" && request.url.endsWith("/decisions")) sent = (await request.clone().json()) as typeof sent;
    };
    server.events.on("request:start", listen);
    const user = userEvent.setup();
    renderConsole(`/investigations/${S1_INVESTIGATION_ID}`);
    const decision = await screen.findByRole("region", { name: "Decision" });
    expect(decision).toHaveTextContent("Decision · Round 1");
    expect(within(decision).queryByTestId("add-evidence")).not.toBeInTheDocument();

    await user.click(within(decision).getByRole("button", { name: "Request more evidence" }));
    const box = within(decision).getByTestId("add-evidence");
    const submit = within(box).getByRole("button", { name: "Add evidence and run round 2" });
    expect(submit).toBeDisabled();
    expect(box).toHaveTextContent("Type the evidence to add.");
    expect(within(box).getByLabelText("Attach a bench printout (optional)")).toBeInTheDocument();

    await user.type(within(box).getByLabelText("Evidence to add"), NOTE);
    expect(submit).toBeEnabled();
    await user.click(submit);

    const next = await screen.findByText("Decision · Round 2", { exact: false }, { timeout: 5000 });
    expect(next).toBeInTheDocument();
    expect(sent).toMatchObject({ kind: "requestInfo", comment: NOTE });
    expect(JSON.stringify(sent)).not.toContain("More evidence requested.");
    const rounds = await screen.findByTestId("round-history", {}, { timeout: 5000 });
    const first = await within(rounds).findByTestId("round-1", {}, { timeout: 5000 });
    expect(first).toHaveTextContent("Round 1 · locked");
    expect(first).toHaveTextContent("Closed: more evidence added");
    expect(first).toHaveTextContent("Evidence added; the agents ran the next round by Bryan Weis");
    expect(first).toHaveTextContent(NOTE);
    expect(within(rounds).getByTestId("round-2")).toHaveTextContent("Round 2 · current");
    const pack = await screen.findByRole("table", { name: "Evidence pack" }, { timeout: 5000 });
    expect(await within(pack).findByTestId("user-provided")).toHaveTextContent("User-provided: Bryan Weis");
    expect(pack).toHaveTextContent("Added by a person");
    expect(pack).toHaveTextContent(NOTE);
    expect(await screen.findByRole("button", { name: "Approve CAPA" })).toBeEnabled();
    server.events.removeListener("request:start", listen);
  });

  it("an escalated round shows Add evidence straight away and reads Waiting for more evidence", async () => {
    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const decision = await screen.findByRole("region", { name: "Decision" });
    expect(within(decision).getByTestId("escalation-gate")).toHaveTextContent("Waiting for more evidence");
    expect(within(decision).getByTestId("add-evidence")).toBeInTheDocument();
    expect(within(decision).queryByRole("button", { name: "Request more evidence" })).not.toBeInTheDocument();
    expect(within(decision).getByRole("button", { name: "Override with justification" })).toBeInTheDocument();
    expect(decision).toHaveTextContent("Stage: Waiting for more evidence");
    expect(decision.textContent).not.toMatch(/Stage: Escalated/);
  });

  it("a past round is locked history with a link to the current round", async () => {
    signedIn();
    const user = userEvent.setup();
    const view = renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const decision = await screen.findByRole("region", { name: "Decision" });
    await user.type(within(decision).getByLabelText("Evidence to add"), NOTE);
    await user.click(within(decision).getByRole("button", { name: "Add evidence and run round 2" }));
    await screen.findByText("Decision · Round 2", { exact: false }, { timeout: 5000 });
    view.unmount();

    renderConsole(`/investigations/${S2_INVESTIGATION_ID}`);
    const banner = await screen.findByTestId("past-round", {}, { timeout: 5000 });
    expect(banner).toHaveTextContent("This is round 1, kept as history.");
    expect(within(banner).getByRole("link", { name: "Go to round 2, the current round" })).toBeInTheDocument();
    const locked = screen.getByRole("region", { name: "Decision" });
    expect(within(locked).getByTestId("decision-record")).toHaveTextContent("Round 1 is closed and kept as history.");
    expect(within(locked).queryByRole("button")).not.toBeInTheDocument();
    expect(within(locked).queryByTestId("add-evidence")).not.toBeInTheDocument();
  });

  it("stops offering Add evidence at the round cap", () => {
    const base = investigationFor(S1_INVESTIGATION_ID) as Investigation;
    render(
      <MemoryRouter>
        <AddEvidenceBox investigation={{ ...base, round: 5, maxRounds: 5 }} pending={false} onSubmit={() => undefined} />
      </MemoryRouter>,
    );
    expect(screen.getByTestId("add-evidence-cap")).toHaveTextContent("This ticket has reached 5 rounds, the most allowed.");
    expect(screen.queryByLabelText("Evidence to add")).not.toBeInTheDocument();
  });
});
