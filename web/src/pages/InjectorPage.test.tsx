// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { SITE_STORAGE_KEY } from "../sites/selection";
import { server } from "../test/server";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

describe("fault injector", () => {
  it("opens the tool trace after inject", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    expect(await screen.findByRole("heading", { name: "Fault injector" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Inject" }));
    expect(await screen.findByRole("heading", { name: /Tool D-01/ })).toBeInTheDocument();
    expect(screen.getByRole("status", { name: "Streaming trace" })).toHaveTextContent(/streaming/i);
    expect(screen.queryByRole("heading", { name: "Alerts" })).not.toBeInTheDocument();
  });

  it("runs a named scenario and opens its tool trace", async () => {
    const user = userEvent.setup();
    renderConsole("/injector");
    const harness = await expandHarness();
    await within(harness).findByRole("option", { name: /SC-4/ });
    await user.selectOptions(within(harness).getByRole("combobox", { name: "Scenario" }), "SC-4");
    await user.click(within(harness).getByRole("button", { name: "Run scenario" }));
    expect(await screen.findByRole("heading", { name: /Tool E-01/ })).toBeInTheDocument();
    expect(screen.getByRole("status", { name: "Streaming trace" })).toHaveTextContent(/streaming/i);
    expect(screen.queryByText("corr-scenario-SC-4")).not.toBeInTheDocument();
  });

  it("has no preset button and no lifecycle breadcrumb row", async () => {
    renderConsole("/injector");
    expect(await screen.findByRole("heading", { name: "Fault injector" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Preset S4/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("list", { name: /lifecycle/i })).not.toBeInTheDocument();
  });

  it("cascades its own Site, then Tool, then Parameter, independent of the console plant", async () => {
    const tool = (id: string, family: string, siteId: string) => ({ id, name: id, family, model: "M", bay: "Bay", chambers: [{ id: `${id}-A`, name: "Chamber A" }], siteId });
    const bySite: Record<string, unknown[]> = {
      northwater: [tool("D-01", "deposition", "northwater"), tool("E-01", "etch", "northwater")],
      ironwood: [tool("I-E-01", "etch", "ironwood"), tool("I-D-01", "deposition", "ironwood")],
      lumen: [tool("P-C-01", "clean", "lumen")],
    };
    const asked: string[] = [];
    server.use(
      http.get("/api/tools", ({ request }) => {
        const site = new URL(request.url).searchParams.get("siteId") ?? "all";
        asked.push(site);
        return HttpResponse.json(site === "all" ? Object.values(bySite).flat() : bySite[site] ?? []);
      }),
    );
    localStorage.setItem(SITE_STORAGE_KEY, "lumen");
    const user = userEvent.setup();
    renderConsole("/injector");
    const siteBox = await screen.findByRole("combobox", { name: "Site" });
    expect(siteBox).toHaveValue("northwater");
    const toolBox = screen.getByRole("combobox", { name: "Tool" });
    await within(toolBox).findByRole("option", { name: /D-01/ });
    expect(within(toolBox).getAllByRole("option")).toHaveLength(2);

    await user.selectOptions(siteBox, "ironwood");
    await within(toolBox).findByRole("option", { name: /I-E-01/ });
    expect(within(toolBox).getAllByRole("option").map((option) => option.getAttribute("value"))).toEqual(["I-E-01", "I-D-01"]);
    expect(asked).not.toContain("lumen");

    const paramBox = screen.getByRole("combobox", { name: "Parameter" });
    const etchParams = within(paramBox).getAllByRole("option").map((option) => option.getAttribute("value"));
    expect(etchParams.length).toBeGreaterThan(0);
    await user.selectOptions(toolBox, "I-D-01");
    const depParams = within(paramBox).getAllByRole("option").map((option) => option.getAttribute("value"));
    expect(depParams.length).toBeGreaterThan(0);
    expect(depParams).not.toEqual(etchParams);
    expect(depParams).toContain((paramBox as HTMLSelectElement).value);
  });
});
