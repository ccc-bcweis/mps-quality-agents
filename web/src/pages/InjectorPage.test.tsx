// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("fault injector", () => {
  it("opens the tool trace after inject", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    expect(await screen.findByRole("heading", { name: "Fault injector" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Inject" }));
    expect(await screen.findByRole("heading", { name: /Tool S-01/ })).toBeInTheDocument();
    expect(screen.getByRole("status", { name: "Streaming trace" })).toHaveTextContent(/streaming/i);
    expect(screen.queryByRole("heading", { name: "Alerts" })).not.toBeInTheDocument();
  });

  it("runs a named scenario and opens its tool trace", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    await screen.findByRole("option", { name: /SC-4/ });
    await user.selectOptions(screen.getByRole("combobox", { name: "Scenario" }), "SC-4");
    await user.click(screen.getByRole("button", { name: "Run scenario" }));
    expect(await screen.findByRole("heading", { name: /Tool E-01/ })).toBeInTheDocument();
    expect(screen.getByRole("status", { name: "Streaming trace" })).toHaveTextContent(/streaming/i);
    expect(screen.queryByText("corr-scenario-SC-4")).not.toBeInTheDocument();
  });

  it("preset S4 opens the first etch tool trace", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    await screen.findByRole("option", { name: /S-01/ });
    await user.click(screen.getByRole("button", { name: /Preset S4/ }));
    expect(await screen.findByRole("heading", { name: /Tool E-01/ })).toBeInTheDocument();
    expect(screen.getByRole("status", { name: "Streaming trace" })).toHaveTextContent(/streaming/i);
    expect(screen.queryByText(/corr-inject-/)).not.toBeInTheDocument();
  });
});
