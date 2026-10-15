// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
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
