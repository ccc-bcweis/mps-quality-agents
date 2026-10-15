// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { incidents, tickets } from "../mocks/demoData";
import { renderConsole } from "../test/render";
import { server } from "../test/server";

describe("rules and process", () => {
  it("shows the rules in plain language and highlights the ticket step on the future map", async () => {
    server.use(
      http.get("/api/tickets", () => HttpResponse.json(tickets)),
      http.get("/api/incidents", () => HttpResponse.json(incidents)),
    );
    const user = userEvent.setup();
    renderConsole("/rules");

    expect(await screen.findByRole("heading", { name: "Rules & Process" })).toBeInTheDocument();
    expect(await screen.findByText("When the severity is critical.")).toBeInTheDocument();
    expect(screen.getByText(/Open a ticket, set the priority to critical/)).toBeInTheDocument();
    expect(screen.getByText(/A critical signal can affect the wafers or the tool/)).toBeInTheDocument();
    expect(screen.queryByText("rule-critical-human")).not.toBeInTheDocument();
    expect(screen.queryByText("TKT-1038")).not.toBeInTheDocument();
    expect(screen.queryByText("TKT-1044")).not.toBeInTheDocument();

    expect(screen.getByRole("button", { name: "Current state" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText("Someone notices the excursion")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Future state" }));
    expect(screen.getByRole("button", { name: "Future state" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText(/An escalated case cannot skip this step/)).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: /Supplier notice: gas lot NF-3317 moisture out of spec/ }));
    const step = screen.getByText("A rule opens the ticket").closest("[data-highlighted]");
    expect(step).toHaveAttribute("data-highlighted", "true");
    expect(screen.getByText("The investigation gathers evidence").closest("[data-highlighted]")).toHaveAttribute(
      "data-highlighted",
      "false",
    );
  });
});
