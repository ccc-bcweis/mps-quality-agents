// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { S1_INVESTIGATION_ID, S2_INVESTIGATION_ID } from "../mocks/demoData";
import { renderConsole } from "../test/render";

const GUID = /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/i;
const ISO = /\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}/;

function visibleCopy(container: HTMLElement): string {
  const clone = container.cloneNode(true) as HTMLElement;
  clone.querySelectorAll("details, [data-audit]").forEach((node) => node.remove());
  return clone.textContent ?? "";
}

async function open(path: string) {
  const view = renderConsole(path);
  if (path === "/capas") await screen.findByRole("heading", { name: "CAPA" });
  else if (path === "/alerts" || path === "/tickets") await screen.findAllByRole("heading", { level: 1 });
  else await screen.findAllByRole("table", { name: "Evidence pack" });
  return view;
}

describe("operator surfaces hide raw identifiers", () => {
  it.each([
    "/investigations/" + S1_INVESTIGATION_ID,
    "/investigations/" + S2_INVESTIGATION_ID,
    "/approvals/" + S1_INVESTIGATION_ID,
    "/alerts",
    "/tickets",
    "/capas",
  ])("%s shows no raw guid or ISO timestamp", async (path) => {
    const view = await open(path);
    const text = visibleCopy(view.container);
    expect(text.match(GUID)?.[0] ?? null).toBeNull();
    expect(text.match(ISO)?.[0] ?? null).toBeNull();
  });

  it("prints a readable local time on the investigation", async () => {
    const view = await open(`/investigations/${S1_INVESTIGATION_ID}`);
    expect(visibleCopy(view.container)).toMatch(/Oct \d+, \d+:\d+ [AP]M/);
  });
});
