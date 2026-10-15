// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../test/server";
import { renderConsole } from "../test/render";

describe("reset demo", () => {
  it("asks what is reset and restores the baseline", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    await user.click(await screen.findByRole("button", { name: "Reset demo" }));
    const dialog = screen.getByRole("dialog", { name: "Reset the demo?" });
    expect(dialog).toHaveTextContent("Fleet state");
    expect(dialog).toHaveTextContent("Tickets");
    expect(dialog).toHaveTextContent("Investigations");
    expect(dialog).toHaveTextContent("Approvals and corrective actions");
    expect(dialog).toHaveTextContent("Simulated clock");
    expect(dialog).toHaveTextContent("The same seed is used on this computer and in the hosted demo.");
    await user.click(screen.getByRole("button", { name: "Confirm reset" }));
    expect(await screen.findByText("Demo baseline restored. Seed 4242.")).toBeInTheDocument();
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
