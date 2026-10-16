// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";

describe("sites overview", () => {
  it("puts Sites before Fleet and shows the Northwater floor", async () => {
    const user = userEvent.setup();
    renderConsole("/sites?site=northwater");

    const nav = await screen.findByRole("navigation", { name: "Console" });
    const labels = within(nav).getAllByRole("link").map((link) => link.textContent ?? "");
    expect(labels.indexOf("Sites")).toBeGreaterThanOrEqual(0);
    expect(labels.indexOf("Sites")).toBeLessThan(labels.indexOf("Production line"));

    expect(await screen.findByRole("heading", { name: "Sites" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Northwater Fab", selected: true })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Ironwood Fab" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Lumen Pilot Works" })).toBeInTheDocument();

    const throughput = screen.getByRole("region", { name: "Northwater Fab throughput" });
    expect(throughput).toHaveTextContent("57.9 per hour");
    expect(throughput).toHaveTextContent("140 wafers");
    const structure = await screen.findByRole("region", { name: "Plant structure" });
    expect(await within(structure).findByText("Deposition tool in bay one")).toBeInTheDocument();
    expect(within(structure).getByText("Film check bench")).toBeInTheDocument();
    expect(within(structure).getByText(/Attached directly to manufacturing/)).toBeInTheDocument();
    expect(within(structure).getAllByText("No live data").length).toBeGreaterThan(1);
    expect(within(structure).queryByText("nw-manufacturing")).not.toBeInTheDocument();
    expect(within(structure).queryByText("D-01")).not.toBeInTheDocument();

    expect(screen.getByText("Cleanroom bay 3")).toBeInTheDocument();
    expect(screen.getByText(/Deposited wafers move to bay 1 for etch/)).toBeInTheDocument();

    const nominal = screen.getByRole("link", { name: /Deposition D-01/ });
    expect(nominal).toHaveAttribute("data-status", "nominal");
    expect(nominal).toHaveTextContent("Nominal");
    const quiet = screen.getByRole("link", { name: /Deposition D-03/ });
    expect(quiet).toHaveAttribute("data-status", "nominal");
    expect(quiet).toHaveTextContent("Nominal");

    expect(screen.queryByRole("region", { name: "Cross-plant comparison" })).not.toBeInTheDocument();
    expect(screen.queryByText("escTemperature")).not.toBeInTheDocument();

    const wip = within(throughput).getByText(matchCopy(/work in process \(WIP\)/));
    await user.click(wip);
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/started and have not finished/i);

    await user.click(within(throughput).getByText("Yield"));
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/met the requirement/i);

    await user.click(screen.getByRole("tab", { name: "Ironwood Fab" }));
    const ironwood = await screen.findByRole("region", { name: "Plant structure" });
    expect(await within(ironwood).findByText("Ironwood deposition tool, first")).toBeInTheDocument();
    expect(within(ironwood).queryByText("Deposition tool in bay one")).not.toBeInTheDocument();
    const watch = await screen.findByRole("link", { name: /Etch I-E-01/ });
    expect(watch).toHaveAttribute("data-status", "warning");
    expect(watch).toHaveTextContent("Watch");
  });

  it("opens the tool schematic from a tool on the floor", async () => {
    const user = userEvent.setup();
    renderConsole("/sites?site=northwater");
    await user.click(await screen.findByRole("link", { name: /Deposition D-01/ }));
    expect(await screen.findByRole("heading", { name: /Tool D-01/ })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: /thin-film deposition schematic/i })).toBeInTheDocument();
  });

  it("names the pilot line at Lumen", async () => {
    const user = userEvent.setup();
    renderConsole("/sites?site=lumen");
    expect(await screen.findByRole("heading", { name: "Lumen Pilot Works" })).toBeInTheDocument();
    expect(screen.getByText("Pilot line bay 3")).toBeInTheDocument();
    expect(await within(await screen.findByRole("region", { name: "Plant structure" })).findByText("Lumen deposition leg")).toBeInTheDocument();
    expect(screen.getAllByText("Pilot line bay 3")).toHaveLength(1);
    const pilot = screen.getAllByText(matchCopy(/pilot line/i))[0];
    expect(pilot).toBeTruthy();
    if (!pilot) return;
    await user.click(pilot);
    expect(await screen.findByRole("tooltip")).toHaveTextContent(/research/i);
  });
});
