// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import type { EvidenceItem } from "../api/types";
import { server } from "../test/server";
import { EvidencePack } from "./EvidencePack";

const FOCUS = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

const linked: EvidenceItem = {
  id: "11111111-1111-4111-8111-111111111112",
  source: "telemetry",
  title: "ESC temperature series",
  excerpt: "The chuck warmed up.",
  observedAt: "2026-10-14T14:08:00Z",
  toolId: "E-02",
  toolName: "Etch tool E-02",
  chamberName: "Chamber B",
  signalId: "escTemperature",
  value: 53.1,
  unit: "C",
  specLow: 40,
  specHigh: 60,
  sourceEventIds: [FOCUS],
};

const unlinked: EvidenceItem = {
  ...linked,
  id: "11111111-1111-4111-8111-111111111199",
  title: "Bench note",
  signalId: null,
  sourceEventIds: [],
};

describe("causal trace panel", () => {
  it("renders the chain in plain language and hides raw ids until technical details", async () => {
    const user = userEvent.setup();
    render(<EvidencePack items={[linked]} />);
    await user.click(screen.getByRole("button", { name: /Show the cause for/ }));
    const dialog = await screen.findByRole("dialog", { name: "Causal trace" });
    const timeline = within(dialog).getByRole("list", { name: "Cause timeline" });
    expect(timeline).toHaveTextContent("Scenario Edge ring revision C on Etch 02 started.");
    expect(timeline).toHaveTextContent("Chuck temperature was 53.1.");
    expect(timeline).toHaveTextContent("The business rule Edge ring temperature fired.");
    expect(timeline).toHaveTextContent("Value 53.1, limit 40 to 60");
    expect(timeline).toHaveTextContent("Etch 02");
    expect(timeline).not.toHaveTextContent(FOCUS);
    expect(timeline).not.toHaveTextContent("telemetry.reading");
    const details = within(dialog).getByText("Technical details").closest("details");
    expect(details).not.toBeNull();
    expect(details).not.toHaveAttribute("open");
    expect(details).toHaveTextContent(FOCUS);
    await user.click(within(dialog).getByText("Technical details"));
    expect(details).toHaveAttribute("open");
  });

  it("says the chain is not on the bus when the evidence has no event", async () => {
    const user = userEvent.setup();
    render(<EvidencePack items={[unlinked]} />);
    await user.click(screen.getByRole("button", { name: /Show the cause for/ }));
    const dialog = await screen.findByRole("dialog", { name: "Causal trace" });
    expect(dialog).toHaveTextContent("The chain is not on the bus yet.");
    expect(dialog).not.toHaveTextContent(unlinked.id);
  });

  it("says the chain was not found without the raw id", async () => {
    server.use(http.get("/api/trace/:eventId", () => HttpResponse.json({ title: "Not found" }, { status: 404 })));
    const user = userEvent.setup();
    render(<EvidencePack items={[linked]} />);
    await user.click(screen.getByRole("button", { name: /Show the cause for/ }));
    const dialog = await screen.findByRole("dialog", { name: "Causal trace" });
    expect(await within(dialog).findByText("The chain was not found.")).toBeInTheDocument();
    expect(dialog).not.toHaveTextContent(FOCUS);
  });
});
