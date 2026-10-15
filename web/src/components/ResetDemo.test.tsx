// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../test/server";
import { renderConsole } from "../test/render";

const RESET_COPY = "Reset the demo? This clears faults, alerts, tickets, investigations and CAPAs.";

describe("reset demo", () => {
  it("asks what is reset and restores a nominal console", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const reset = await screen.findByRole("button", { name: "Reset demo" });
    expect(reset.className).toContain("bg-sn-gold");
    expect(reset.className).toContain("text-sn-gold-ink");
    await user.click(reset);
    const dialog = screen.getByRole("dialog", { name: RESET_COPY });
    expect(dialog).toHaveTextContent(RESET_COPY);
    await user.click(screen.getByRole("button", { name: "Confirm reset" }));
    expect(await screen.findByText("Demo baseline restored. Seed 4242.")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Fleet overview" })).toBeInTheDocument();
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
    expect(await screen.findByText("reader")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Account menu" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Reset demo" })).not.toBeInTheDocument();
  });
});
