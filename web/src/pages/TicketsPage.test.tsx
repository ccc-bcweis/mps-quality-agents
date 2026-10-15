// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { incidents, tickets } from "../mocks/demoData";
import { server } from "../test/server";
import { renderConsole } from "../test/render";

function showFixtureTickets() {
  server.use(
    http.get("/api/tickets", () => HttpResponse.json(tickets)),
    http.get("/api/incidents", () => HttpResponse.json(incidents)),
  );
}

describe("tickets", () => {
  it("starts with an empty queue", async () => {
    renderConsole("/tickets");
    expect(await screen.findByRole("heading", { name: "Tickets" })).toBeInTheDocument();
    expect(screen.queryByText("TKT-1044")).not.toBeInTheDocument();
    expect(screen.queryByText("TKT-1042")).not.toBeInTheDocument();
    expect(screen.queryByText("TKT-1038")).not.toBeInTheDocument();
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Ticket");
  });

  it("opens the investigation that already belongs to the ticket", async () => {
    showFixtureTickets();
    const user = userEvent.setup();
    renderConsole("/tickets");
    expect(await screen.findByText("TKT-1044")).toBeInTheDocument();

    await user.click(screen.getByText("ESC temperature oscillation after a recipe revision."));

    expect(await screen.findByRole("heading", { name: "Investigation" })).toBeInTheDocument();
    expect(await screen.findByRole("table", { name: "Evidence pack" })).toHaveTextContent("Product records");
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Start investigation" })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Tickets" })).not.toBeInTheDocument();
  });

  it("starts an investigation when the ticket does not have one yet", async () => {
    showFixtureTickets();
    const user = userEvent.setup();
    renderConsole("/tickets");
    expect(await screen.findByText("TKT-1038")).toBeInTheDocument();

    await user.click(screen.getByText("Supplier notice: gas lot NF-3317 moisture out of spec."));

    expect(await screen.findByRole("heading", { name: "Investigation" })).toBeInTheDocument();
    expect(screen.getByText(/No investigation has started for this ticket/)).toBeInTheDocument();
    expect(screen.getByText(/gas lot NF-3317/)).toBeInTheDocument();
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Start investigation" }));

    const steps = await screen.findByRole("list", { name: "Agent steps" });
    expect(within(steps).getByText("Triage").closest("li")).toHaveAttribute("data-status", "running");
    expect(screen.queryByText("No such record.")).not.toBeInTheDocument();
  });

  it("lists only the selected tool's sensor channels", async () => {
    const user = userEvent.setup();
    renderConsole("/tickets");
    const report = await screen.findByRole("heading", { name: "Operator report" });
    const form = report.closest("form");
    expect(form).not.toBeNull();
    const tool = within(form as HTMLElement).getByLabelText("Tool");
    await user.selectOptions(tool, "E-01");

    const parameter = within(form as HTMLElement).getByLabelText("Parameter");
    expect(within(parameter).getAllByRole("option", { name: /ESC temperature \(C\), spec 40-60/ }).length).toBeGreaterThan(0);
    expect(within(parameter).getAllByRole("option", { name: /Run rate \(wafers\/hour\), spec 8-14/ }).length).toBeGreaterThan(0);
    expect(within(parameter).queryByRole("option", { name: /RUN-RATE/ })).not.toBeInTheDocument();
    expect(within(parameter).queryByRole("option", { name: /Film thickness/ })).not.toBeInTheDocument();

    await user.selectOptions(tool, "D-01");
    expect(within(parameter).queryByRole("option", { name: /ESC temperature/ })).not.toBeInTheDocument();
    expect(within(parameter).getAllByRole("option", { name: /RF power \(W\), spec 300-600/ }).length).toBeGreaterThan(0);
  });

  it("confirms an oscilloscope printout and shows the alert", async () => {
    const user = userEvent.setup();
    renderConsole("/tickets");
    expect(await screen.findByRole("heading", { name: "Upload telemetry" })).toBeInTheDocument();
    const file = new File([new Uint8Array([37, 80, 68, 70])], "oscilloscope-e01.pdf", { type: "application/pdf" });
    await user.upload(screen.getByLabelText("Printout"), file);

    expect(await screen.findByDisplayValue("2140")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Confirm reading" }));
    expect(await screen.findByText(/An oscillation was applied to Source RF power on E-01 and an alert was opened/)).toBeInTheDocument();
    expect(screen.getByText(/Manual measurement: oscilloscope on E-01, rfPower 2140 W/)).toBeInTheDocument();
  });
});
