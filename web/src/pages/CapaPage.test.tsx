// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { matchCopy } from "../test/matchCopy";
import { renderConsole } from "../test/render";

describe("capa", () => {
  it("starts empty and marks the CAPA step", async () => {
    renderConsole("/capas");
    expect(await screen.findByRole("heading", { name: "CAPA" })).toBeInTheDocument();
    expect(screen.getByText(matchCopy(/No CAPA yet/))).toBeInTheDocument();
  });
});

describe("capa after a reset elsewhere", () => {
  it("drops CAPAs remembered in this tab when the server list is empty, and shows no other-plant banner", async () => {
    const stale = [
      { id: "CAPA-OLD-1", incidentId: "INC-1", toolId: "E-02", stage: "closed", summary: "PL-2044 CF4", openedAt: "2026-09-01T00:00:00Z" },
      { id: "CAPA-OLD-2", incidentId: "INC-2", toolId: "E-01", stage: "closed", summary: "PL-2044 CF4", openedAt: "2026-08-31T00:00:00Z" },
    ];
    sessionStorage.setItem("mps.capas", JSON.stringify(stale));
    renderConsole("/capas");
    expect(await screen.findByText(matchCopy(/No CAPA yet/))).toBeInTheDocument();
    expect(screen.queryByText(/PL-2044/)).not.toBeInTheDocument();
    expect(screen.queryByText(/at another plant/)).not.toBeInTheDocument();
    await waitFor(() => expect(sessionStorage.getItem("mps.capas")).toBeNull());
  });
});
