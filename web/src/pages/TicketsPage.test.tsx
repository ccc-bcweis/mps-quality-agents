// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
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
});
