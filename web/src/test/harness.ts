// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

/** The harness starts collapsed. Presenter controls are behind Expand harness. */
export async function expandHarness() {
  const harness = await screen.findByRole("banner", { name: "StatusNeo demo harness" });
  const expand = within(harness).queryByRole("button", { name: "Expand harness" });
  if (expand) await userEvent.setup().click(expand);
  return harness;
}
