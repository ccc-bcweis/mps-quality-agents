// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../test/server";
import { expandHarness } from "../test/harness";
import { renderConsole } from "../test/render";

function status(body: Record<string, unknown>) {
  server.use(http.get("/api/model/status", () => HttpResponse.json(body)));
}

describe("mode banner shows the model the server really uses", () => {
  it("offline: saved answers, no toggle, zero spend", async () => {
    renderConsole("/");
    await expandHarness();
    const engine = await screen.findByRole("group", { name: "AI engine" });
    expect(engine.className).not.toMatch(/truncate|overflow-hidden|min-w-0/);
    expect(await screen.findByRole("status", { name: "AI engine in use" })).toHaveTextContent("Offline (saved answers)");
    expect(screen.queryByRole("button", { name: /Live \(real model\)/ })).not.toBeInTheDocument();
    expect(screen.getByText("Answers only. Plant data stays the same.")).toBeInTheDocument();
    expect(await screen.findByText("$0.00 of $2.00")).toBeInTheDocument();
  });

  it("openai: names the provider and the model", async () => {
    status({ mode: "openai", label: "OpenAI", model: "gpt-test", live: true, fallingBack: false, detail: null });
    renderConsole("/");
    await expandHarness();
    expect(await screen.findByRole("status", { name: "AI engine in use" })).toHaveTextContent("OpenAI · gpt-test");
  });

  it("azure falling back: says so in plain words", async () => {
    status({
      mode: "azure",
      label: "Azure OpenAI",
      model: "dep",
      live: true,
      fallingBack: true,
      detail: "The model service could not be reached. Using saved answers until the model answers again.",
    });
    renderConsole("/");
    await expandHarness();
    expect(await screen.findByRole("status", { name: "AI engine in use" })).toHaveTextContent("Azure OpenAI · dep");
    expect(await screen.findByText(/could not be reached\. Using saved answers/)).toBeInTheDocument();
  });

  it("placeholder: labeled and explained", async () => {
    status({ mode: "placeholder", label: "Placeholder", live: false, fallingBack: false, detail: "A fixed placeholder answers every investigation." });
    renderConsole("/");
    await expandHarness();
    expect(await screen.findByRole("status", { name: "AI engine in use" })).toHaveTextContent("Placeholder");
    expect(await screen.findByText(/fixed placeholder answers every investigation/)).toBeInTheDocument();
  });
});
