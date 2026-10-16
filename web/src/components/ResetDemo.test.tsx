// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../test/server";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

const RESET_COPY =
  "Hard reset wipes tickets, alerts, investigations, and history, then fills a week of healthy readings. Company rules stay.";

describe("reset demo", () => {
  it("asks what is reset and restores a nominal console", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    await expandHarness();
    const reset = await screen.findByRole("button", { name: "Hard reset" });
    expect(reset.className).toContain("bg-sn-gold");
    expect(reset.className).toContain("text-sn-gold-ink");
    await user.click(reset);
    const dialog = screen.getByRole("dialog", { name: RESET_COPY });
    expect(dialog).toHaveTextContent(RESET_COPY);
    await user.click(screen.getByRole("button", { name: "Confirm hard reset" }));
    expect(await screen.findByText("Demo baseline restored. Seed 4242.")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Production line overview" })).toBeInTheDocument();
    expect((await screen.findByRole("link", { name: /D-01/ })).textContent).toMatch(/NOMINAL/);
    expect(screen.getByRole("link", { name: /E-02/ }).textContent).toMatch(/NOMINAL/);
    expect(screen.queryByText("ALARM")).not.toBeInTheDocument();
  });

  it("hides the reset action when the caller is not an admin", async () => {
    server.use(
      http.get("/api/admin/session", () =>
        HttpResponse.json({ mode: "azure", isAdmin: false, displayName: "reader" }),
      ),
    );
    renderConsole("/");
    await expandHarness();
    expect(await screen.findByText("reader")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Account menu" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Hard reset" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Soft reset" })).not.toBeInTheDocument();
  });
});
