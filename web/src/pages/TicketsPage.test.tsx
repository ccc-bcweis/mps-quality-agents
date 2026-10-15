// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("tickets", () => {
  it("lists open tickets and marks the ticket step", async () => {
    renderConsole("/tickets");
    expect(await screen.findByRole("heading", { name: "Tickets" })).toBeInTheDocument();
    expect(await screen.findByText("TKT-1044")).toBeInTheDocument();
    expect(screen.getByText("TKT-1042")).toBeInTheDocument();
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Ticket");
  });

  it("opens the investigation that already belongs to the ticket", async () => {
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
});
