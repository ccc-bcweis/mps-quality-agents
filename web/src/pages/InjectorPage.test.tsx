// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("fault injector", () => {
  it("shows the new alert after inject", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    expect(await screen.findByRole("heading", { name: "Fault injector" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Inject" }));
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    expect(await screen.findByText(/corr-inject-/)).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveTextContent(/new alert/i);
  });

  it("preset S4 raises an alert on both etch tools", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    await screen.findByRole("option", { name: /S-01/ });
    await user.click(screen.getByRole("button", { name: /Preset S4/ }));
    expect(await screen.findByRole("heading", { name: "Alerts" })).toBeInTheDocument();
    const correlations = await screen.findAllByText(/corr-inject-/);
    expect(correlations.length).toBeGreaterThanOrEqual(2);
  });
});
