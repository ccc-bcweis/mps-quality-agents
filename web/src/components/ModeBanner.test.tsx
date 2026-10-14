// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

describe("mode banner and spend", () => {
  it("starts in replay at zero spend and previews live without hiding the gateway mode", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    expect(await screen.findByText("REPLAY MODE")).toBeInTheDocument();
    expect(await screen.findByText("$0.00 of $2.00")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Switch to LIVE AI" }));
    expect(screen.getByText("LIVE AI")).toBeInTheDocument();
    expect(screen.getByText(/Gateway reports replay/)).toBeInTheDocument();
  });
});
