// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { GlossaryScope } from "./GlossaryScope";
import { Term } from "./Term";

function show(node: ReactNode) {
  return render(<GlossaryScope>{node}</GlossaryScope>);
}

describe("glossary term", () => {
  it("spells the term out once, underlines it, and opens the tooltip on focus", async () => {
    const user = userEvent.setup();
    show(
      <>
        <Term id="SPC" />
        <Term id="SPC" />
      </>,
    );
    const spelled = screen.getAllByText(/statistical process control/i);
    expect(spelled.length).toBeGreaterThan(0);
    const tokens = screen.getAllByText("SPC");
    expect(tokens.length).toBe(2);
    for (const token of tokens) {
      expect(token).toHaveClass("mps-term-token");
      expect(token.style.borderBottom).toContain("dotted");
    }
    expect(screen.getAllByRole("generic").some((node) => node.getAttribute("data-first-use") === "false")).toBe(true);
    const first = document.querySelector("[data-first-use='true']");
    expect(first).not.toBeNull();
    await user.click(first as HTMLElement);
    const tip = await screen.findByRole("tooltip");
    expect(tip).toHaveTextContent(/running check/i);
    expect(tip).toHaveTextContent(/opens a ticket/i);
    expect(tip).toHaveTextContent(/Why it matters/i);
  });

  it("spells the first Western Electric rule code from WE and keeps the suffix on the next rule", () => {
    show(
      <>
        <Term id="WE-1" />
        <Term id="WE-2" />
      </>,
    );
    const first = document.querySelector("[data-term='WE'][data-first-use='true']");
    expect(first?.textContent).toMatch(/Western Electric rules \(WE-1\)/);
    const second = document.querySelectorAll("[data-term='WE'][data-first-use='false']");
    expect(second).toHaveLength(1);
    expect(second[0]?.textContent).toBe("WE-2");
  });
});
