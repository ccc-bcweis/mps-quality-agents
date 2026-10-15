// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { api } from "../api/client";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";
import { MISSING_ALERT, MISSING_CAPA, MISSING_INVESTIGATION, MISSING_TICKET } from "./copy";

const gone = "00000000-0000-4000-8000-000000000099";

function rowText(tableName: string): string[] {
  return within(screen.getByRole("table", { name: tableName }))
    .getAllByRole("row")
    .map((row) => row.textContent ?? "");
}

describe("history lists", () => {
  it("lists investigations and tickets newest first with the tool's plain name", async () => {
    renderConsole("/investigations");
    expect(await screen.findByRole("table", { name: "Investigations" })).toBeInTheDocument();
    const investigations = rowText("Investigations");
    const etch = investigations.findIndex((text) => text.includes("Etch 02"));
    const deposition = investigations.findIndex((text) => text.includes("Deposition 01"));
    expect(etch).toBeGreaterThan(0);
    expect(deposition).toBeGreaterThan(etch);

    // The console starts nominal: no tickets until a scenario runs.
    renderConsole("/tickets");
    expect(await screen.findByText("No tickets yet.")).toBeInTheDocument();
  });

  it("opens Investigation and Approval on the lists", async () => {
    renderConsole("/");
    const nav = await screen.findByRole("navigation", { name: "Console" });
    const investigation = within(nav).getByRole("link", { name: "Investigation" });
    const approval = within(nav).getByRole("link", { name: "Approval" });
    expect(investigation).toHaveAttribute("href", "/investigations");
    expect(approval).toHaveAttribute("href", "/approvals");
    expect(investigation.getAttribute("href")).not.toContain(S1_INVESTIGATION_ID);
    expect(approval.getAttribute("href")).not.toContain(S1_INVESTIGATION_ID);
  });

  it("falls back to the list when a deep link misses", async () => {
    renderConsole(`/alerts/${gone}`);
    expect(await screen.findByText(MISSING_ALERT)).toBeInTheDocument();
    expect(screen.getByText("No alerts yet.")).toBeInTheDocument();
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();

    renderConsole(`/tickets/${gone}`);
    expect(await screen.findByText(MISSING_TICKET)).toBeInTheDocument();
    expect(screen.getByText("No tickets yet.")).toBeInTheDocument();
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();

    renderConsole(`/investigations/${gone}`);
    expect(await screen.findByText(MISSING_INVESTIGATION)).toBeInTheDocument();
    expect(await screen.findByRole("table", { name: "Investigations" })).toBeInTheDocument();
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();

    renderConsole(`/approvals/${gone}`);
    expect(await screen.findByText(MISSING_INVESTIGATION)).toBeInTheDocument();
    expect(await screen.findByRole("table", { name: "Approvals" })).toBeInTheDocument();
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();

    renderConsole(`/capas/${gone}`);
    expect(await screen.findByText(MISSING_CAPA)).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/No CAPA yet/))).toBeInTheDocument();
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();
    expect(screen.queryByText("CAPA was not found.")).not.toBeInTheDocument();
  });

  it("filters list endpoints by plant and keeps newest investigations first", async () => {
    expect(await api.listAlerts(undefined, "ironwood")).toEqual([]);
    expect(await api.listInvestigations("ironwood")).toEqual([]);
    expect((await api.listInvestigations()).map((row) => row.id)).toEqual([S1_INVESTIGATION_ID, S2_INVESTIGATION_ID]);
  });
});
