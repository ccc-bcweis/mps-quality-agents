// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { renderConsole } from "../test/render";

const TIP = "This does not change the plant data. It only changes how the AI answers.";

describe("mode banner and spend", () => {
  it("starts in replay at zero spend and previews live without hiding the gateway mode", async () => {
    const user = userEvent.setup();
    renderConsole("/");
    const engine = await screen.findByRole("group", { name: "AI engine" });
    expect(engine).toHaveAttribute("title", TIP);
    expect(engine.className).not.toMatch(/truncate|overflow-hidden|min-w-0/);
    const replay = screen.getByRole("button", { name: "Replay (saved answers, free, repeatable)" });
    const live = screen.getByRole("button", { name: "Live (real model)" });
    expect(replay).toHaveAttribute("aria-pressed", "true");
    expect(replay.className).toContain("whitespace-nowrap");
    expect(live.className).toContain("whitespace-nowrap");
    expect(screen.getByText("Answers only. Plant data stays the same.")).toBeInTheDocument();
    expect(await screen.findByText("$0.00 of $2.00")).toBeInTheDocument();
    await user.click(live);
    expect(live).toHaveAttribute("aria-pressed", "true");
    expect(replay).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByText(/Gateway reports replay/)).toBeInTheDocument();
  });
});
