// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";

describe("sites overview", () => {
  it("puts Sites before Fleet and shows the Northwater floor", async () => {
    const user = userEvent.setup();
    renderConsole("/sites");

    const nav = await screen.findByRole("navigation", { name: "Console" });
    const labels = within(nav).getAllByRole("link").map((link) => link.textContent ?? "");
    expect(labels.indexOf("Sites")).toBeGreaterThanOrEqual(0);
    expect(labels.indexOf("Sites")).toBeLessThan(labels.indexOf("Fleet"));

    expect(await screen.findByRole("heading", { name: "Sites" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Northwater Fab", selected: true })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Ironwood Fab" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Lumen Pilot Works" })).toBeInTheDocument();

    const throughput = screen.getByRole("region", { name: "Northwater Fab throughput" });
    expect(throughput).toHaveTextContent("57.9 per hour");
    expect(throughput).toHaveTextContent("140 wafers");
    expect(screen.getByText("Bay 3 deposition")).toBeInTheDocument();
    expect(screen.getByText(/Deposited wafers move to bay 1 for etch/)).toBeInTheDocument();

    const nominal = screen.getByRole("link", { name: /Deposition D-01/ });
    expect(nominal).toHaveAttribute("data-status", "nominal");
    expect(nominal).toHaveTextContent("Nominal");
    const alarm = screen.getByRole("link", { name: /Deposition D-03/ });
    expect(alarm).toHaveAttribute("data-status", "alarm");
    expect(alarm).toHaveTextContent("Alarm");

    expect(screen.getByText(/12 degrees hotter/)).toBeInTheDocument();
    expect(screen.getByText(/4\.9 degrees hotter/)).toBeInTheDocument();
    expect(screen.queryByText("escTemperature")).not.toBeInTheDocument();

    const wip = within(throughput).getByText(matchCopy(/work in process \(WIP\)/));
    await user.click(wip);
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/started and have not finished/i);

    await user.click(within(throughput).getByText("Yield"));
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/met the requirement/i);

    await user.click(screen.getByRole("tab", { name: "Ironwood Fab" }));
    const watch = await screen.findByRole("link", { name: /Etch I-E-01/ });
    expect(watch).toHaveAttribute("data-status", "warning");
    expect(watch).toHaveTextContent("Watch");

    await user.selectOptions(screen.getByLabelText("Tool family"), "deposition");
    expect(await screen.findByText(/No parameter on this family sits apart/)).toBeInTheDocument();
    expect(screen.queryByText(/12 degrees hotter/)).not.toBeInTheDocument();
  });

  it("opens the tool schematic from a tool on the floor", async () => {
    const user = userEvent.setup();
    renderConsole("/sites");
    await user.click(await screen.findByRole("link", { name: /Deposition D-01/ }));
    expect(await screen.findByRole("heading", { name: /Tool D-01/ })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: /thin-film deposition schematic/i })).toBeInTheDocument();
  });

  it("names the pilot line at Lumen", async () => {
    const user = userEvent.setup();
    renderConsole("/sites?site=lumen");
    expect(await screen.findByRole("heading", { name: "Lumen Pilot Works" })).toBeInTheDocument();
    expect(screen.getByText("Pilot deposition")).toBeInTheDocument();
    const pilot = screen.getAllByText(matchCopy(/pilot line/i))[0];
    expect(pilot).toBeTruthy();
    if (!pilot) return;
    await user.click(pilot);
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/research/i);
  });
});
