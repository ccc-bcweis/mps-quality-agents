// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LifecycleBar } from "./LifecycleBar";

describe("lifecycle bar", () => {
  it("marks the current step and names an escalation", () => {
    render(<LifecycleBar current="investigation" escalated />);
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Investigation (escalated)");
  });

  it("marks approval when that is the current step", () => {
    render(<LifecycleBar current="approval" escalated={false} />);
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Approval");
  });
});
