// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { ToolFamily } from "../api/types";
import { ToolSchematic } from "../components/ToolSchematic";
import { fleetSensors } from "virtual:fleet-sensors";
import { formatValue } from "./telemetry";
import {
  CHAMBER_MIN_PX,
  COUNTER_MIN_PX,
  HANDLER_PANEL_PX,
  anchorBody,
  anchorMismatch,
  anchorsOutsideView,
  faceplateProblems,
  labelProblems,
  overlappingPairs,
  schematicPanels,
  pointLabel,
  slotWord,
  type SchematicPanel,
} from "./schematic";

const FAMILIES: ToolFamily[] = ["deposition", "etch", "strip", "clean"];

function drawnAnchors(family: ToolFamily): string[] {
  const { container } = render(<ToolSchematic family={family} sensors={[]} selectedId={null} onSelect={() => undefined} />);
  return [...container.querySelectorAll("[data-schematic-anchor]")].map((node) => node.getAttribute("data-schematic-anchor") ?? "");
}

function svgText(container: HTMLElement): string {
  return [...container.querySelectorAll("svg")].map((node) => node.textContent ?? "").join("\n");
}

function faceplateText(anchor: string) {
  const sensors = fleetSensors.filter((sensor) => sensor.schematicAnchor === anchor);
  const id = sensors.map((sensor) => sensor.id).sort((left, right) => right.length - left.length)[0] ?? anchor;
  const value = sensors
    .map((sensor) => {
      // Widest value the sensor can show: its own limits, formatted the way a live reading is.
      const widest = Math.abs(sensor.limits.maximum) >= Math.abs(sensor.limits.minimum) ? sensor.limits.maximum : sensor.limits.minimum;
      return `${formatValue(widest, sensor.unit)} ${sensor.unit}`;
    })
    .sort((left, right) => right.length - left.length)[0] ?? "";
  return { label: `${slotWord(anchor)} ${pointLabel(anchor)}`, id, value };
}

function panelWidth(panel: SchematicPanel): number {
  if (panel.kind === "counters") return COUNTER_MIN_PX;
  if (panel.kind === "handler") return HANDLER_PANEL_PX;
  return CHAMBER_MIN_PX;
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

  it("matches every fleet schematicAnchor to the drawing, and the drawing has no extra anchor", () => {
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

  it("keeps sensor faceplates from overlapping inside each panel", () => {
    for (const family of FAMILIES) {
      for (const panel of schematicPanels(family)) {
        expect(overlappingPairs(panel.boxes)).toEqual([]);
      }
    }
  });

  it("keeps every anchor box inside its panel viewBox", () => {
    for (const family of FAMILIES) {
      for (const panel of schematicPanels(family)) {
        expect(anchorsOutsideView(panel.boxes, panel.view)).toEqual([]);
        expect(anchorsOutsideView(panel.boxes, panel.view, 8)).toEqual([]);
      }
    }
  });

  it("draws chamber B in the same local coordinates as chamber A", () => {
    for (const family of FAMILIES) {
      const chamberA = schematicPanels(family).find((panel) => panel.id === "chamber-a");
      const chamberB = schematicPanels(family).find((panel) => panel.id === "chamber-b");
      if (!chamberA || !chamberB) continue;
      const geometry = (panel: SchematicPanel) =>
        panel.boxes.map((box) => `${anchorBody(box.anchor)}:${box.x}:${box.y}:${box.w}:${box.h}`).sort();
      expect(geometry(chamberA)).toEqual(geometry(chamberB));
    }
  });

  it("keeps hardware captions inside the panel and off the sensor faceplates", () => {
    for (const family of FAMILIES) {
      for (const panel of schematicPanels(family)) {
        expect(labelProblems(panel)).toEqual([]);
      }
    }
  });

  it("fits 11px sensor text in each faceplate at the minimum panel width", () => {
    for (const family of FAMILIES) {
      for (const panel of schematicPanels(family)) {
        expect(faceplateProblems(panel, panelWidth(panel), faceplateText)).toEqual([]);
      }
    }
    const etch = schematicPanels("etch").find((panel) => panel.id === "chamber-a");
    expect(etch).toBeDefined();
    const cramped = faceplateProblems(etch!, 200, () => ({
      label: "A Chamber pressure",
      id: "E-02-A-OUT-PARTICLES",
      value: "45.0 mTorr",
    }));
    expect(cramped.length).toBeGreaterThan(0);
  });

  it("draws the plasma hardware and leaves the RF generator off the clean tool", () => {
    const { container: etch } = render(<ToolSchematic family="etch" sensors={[]} selectedId={null} onSelect={() => undefined} />);
    const etchText = svgText(etch);
    expect(etchText).toMatch(/Showerhead/);
    expect(etchText).toMatch(/Match/);
    expect(etchText).toMatch(/Source/);
    expect(etchText).toMatch(/Bias/);
    expect(etchText).toMatch(/Throttle/);
    expect(etchText).toMatch(/Pump/);
    expect(etchText).toMatch(/Gas box/);
    expect(etchText).toMatch(/Load lock/);
    expect(etchText).toMatch(/Wafer handler/);
    expect(screen.getByRole("heading", { name: "Tool counters" })).toBeInTheDocument();
    expect(etch.querySelector("[data-chamber-grid]")?.getAttribute("style")).toContain(`${CHAMBER_MIN_PX}px`);
    expect(etch.innerHTML).not.toContain("min-w-[1600px]");
    expect(etch.innerHTML).not.toContain("overflow-x-auto");

    const { container: deposition } = render(
      <ToolSchematic family="deposition" sensors={[]} selectedId={null} onSelect={() => undefined} />,
    );
    const depositionText = svgText(deposition);
    expect(depositionText).toMatch(/Showerhead/);
    expect(depositionText).toMatch(/Heater/);
    expect(depositionText).not.toMatch(/Bias/);

    const { container: strip } = render(<ToolSchematic family="strip" sensors={[]} selectedId={null} onSelect={() => undefined} />);
    expect(svgText(strip)).toMatch(/Remote plasma/);

    const { container: clean } = render(<ToolSchematic family="clean" sensors={[]} selectedId={null} onSelect={() => undefined} />);
    const cleanText = svgText(clean);
    expect(cleanText).toMatch(/Bath/);
    expect(cleanText).toMatch(/Spin chuck/);
    expect(cleanText).toMatch(/Megasonic/);
    expect(cleanText).toMatch(/Drain pump/);
    expect(cleanText).not.toMatch(/Match/);
    expect(cleanText).not.toMatch(/\bRF\b/);
    expect(screen.getAllByRole("img", { name: /schematic/i }).length).toBeGreaterThan(0);
    expect(screen.getAllByRole("region", { name: /schematic/i }).length).toBeGreaterThan(0);
  });
});
