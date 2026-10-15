// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { ToolFamily } from "../api/types";
import { ToolSchematic } from "../components/ToolSchematic";
import { fleetSensors } from "virtual:fleet-sensors";
import { anchorBoxes, anchorMismatch, overlappingPairs, SCHEMATIC_VIEW } from "./schematic";

const FAMILIES: ToolFamily[] = ["deposition", "etch", "strip", "clean"];

function drawnAnchors(family: ToolFamily): string[] {
  const { container } = render(
    <ToolSchematic family={family} sensors={[]} selectedId={null} onSelect={() => undefined} />,
  );
  const svg = container.querySelector("svg");
  expect(svg).not.toBeNull();
  return [...svg!.querySelectorAll("[data-schematic-anchor]")].map((node) => node.getAttribute("data-schematic-anchor") ?? "");
}

describe("schematic anchors", () => {
  it("fails when an anchor is only in the catalog or only on the drawing", () => {
    expect(anchorMismatch(["a.chamber.pressure"], ["a.chamber.pressure", "gas.flow"])).toEqual({
      missingOnDrawing: [],
      extraOnDrawing: ["gas.flow"],
    });
    expect(anchorMismatch(["a.chuck.esc"], [])).toEqual({
      missingOnDrawing: ["a.chuck.esc"],
      extraOnDrawing: [],
    });
  });

  it("matches every fleet schematicAnchor to the SVG, and the SVG has no extra anchor", () => {
    for (const family of FAMILIES) {
      const catalog = fleetSensors.filter((sensor) => sensor.family === family);
      const byTool = new Map<string, string[]>();
      for (const sensor of catalog) {
        const list = byTool.get(sensor.toolId) ?? [];
        list.push(sensor.schematicAnchor);
        byTool.set(sensor.toolId, list);
      }
      const sets = [...byTool.values()].map((anchors) => [...new Set(anchors)].sort().join("|"));
      expect(new Set(sets).size).toBe(1);
      const mismatch = anchorMismatch(catalog.map((sensor) => sensor.schematicAnchor), drawnAnchors(family));
      expect(mismatch).toEqual({ missingOnDrawing: [], extraOnDrawing: [] });
    }
  });

  it("keeps sensor faceplates from overlapping at the 1600px drawing size", () => {
    for (const family of FAMILIES) {
      const boxes = anchorBoxes(family);
      expect(overlappingPairs(boxes)).toEqual([]);
      for (const box of boxes) {
        expect(box.x - box.w / 2).toBeGreaterThanOrEqual(8);
        expect(box.x + box.w / 2).toBeLessThanOrEqual(SCHEMATIC_VIEW.width - 8);
        expect(box.y - box.h / 2).toBeGreaterThanOrEqual(8);
        expect(box.y + box.h / 2).toBeLessThanOrEqual(SCHEMATIC_VIEW.height - 8);
      }
    }
  });

  it("draws the plasma hardware and leaves the RF generator off the clean tool", () => {
    const { container: etch } = render(
      <ToolSchematic family="etch" sensors={[]} selectedId={null} onSelect={() => undefined} />,
    );
    const etchText = etch.querySelector("svg")?.textContent ?? "";
    expect(etchText).toMatch(/Showerhead/);
    expect(etchText).toMatch(/Match/);
    expect(etchText).toMatch(/Source/);
    expect(etchText).toMatch(/Bias/);
    expect(etchText).toMatch(/Throttle/);
    expect(etchText).toMatch(/Pump/);
    expect(etchText).toMatch(/Gas box/);
    expect(etchText).toMatch(/Load lock/);
    expect(etchText).toMatch(/Wafer counter/);

    const { container: deposition } = render(
      <ToolSchematic family="deposition" sensors={[]} selectedId={null} onSelect={() => undefined} />,
    );
    const depositionText = deposition.querySelector("svg")?.textContent ?? "";
    expect(depositionText).toMatch(/Showerhead/);
    expect(depositionText).toMatch(/Heater/);
    expect(depositionText).not.toMatch(/Bias/);

    const { container: strip } = render(
      <ToolSchematic family="strip" sensors={[]} selectedId={null} onSelect={() => undefined} />,
    );
    expect(strip.querySelector("svg")?.textContent ?? "").toMatch(/Remote plasma/);

    const { container: clean } = render(
      <ToolSchematic family="clean" sensors={[]} selectedId={null} onSelect={() => undefined} />,
    );
    const cleanText = clean.querySelector("svg")?.textContent ?? "";
    expect(cleanText).toMatch(/Bath/);
    expect(cleanText).toMatch(/Spin chuck/);
    expect(cleanText).toMatch(/Megasonic/);
    expect(cleanText).toMatch(/Drain pump/);
    expect(cleanText).not.toMatch(/Match/);
    expect(cleanText).not.toMatch(/\bRF\b/);
    expect(screen.getAllByRole("img", { name: /schematic/i }).length).toBeGreaterThan(0);
  });
});
