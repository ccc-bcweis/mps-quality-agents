// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import type { EvidenceItem } from "../api/types";
import {
  formatLocalWhen,
  formatMeasure,
  formatStepDuration,
  oneSentence,
  placeLabel,
  plainChamber,
  plainFinding,
  plainNarrative,
  plainTool,
  readableExcerpt,
  valueVsSpec,
  whatItShows,
} from "./format";

const GUID = "11111111-1111-4111-8111-111111111111";

function item(partial: Partial<EvidenceItem> & Pick<EvidenceItem, "title" | "excerpt">): EvidenceItem {
  return {
    id: GUID,
    source: "spc",
    observedAt: "2026-10-14T21:42:00Z",
    ...partial,
  };
}

describe("evidence formatting", () => {
  it("prints a local clock time", () => {
    expect(formatLocalWhen("2026-10-14T09:42:00Z", "UTC")).toBe("Oct 14, 9:42 AM UTC");
    expect(formatLocalWhen("2026-10-14T21:42:00Z", "UTC")).toBe("Oct 14, 9:42 PM UTC");
    expect(formatLocalWhen("2026-10-14T21:42:00Z", "America/New_York")).toBe("Oct 14, 5:42 PM EDT");
    expect(formatLocalWhen("not-a-date", "UTC")).toBe("—");
  });

  it("names the tool and chamber in plain words", () => {
    expect(plainTool("E-02")).toBe("Etch tool E-02");
    expect(plainTool("nope")).toBeNull();
    expect(plainChamber("C2")).toBe("Chamber B");
    expect(plainChamber("B")).toBe("Chamber B");
    expect(placeLabel("E-02", "C2")).toBe("Etch tool E-02, Chamber B");
    expect(placeLabel("E-02", "C2", "Etch tool E-02", "Chamber B")).toBe("Etch tool E-02, Chamber B");
  });

  it("shows the reading against the spec window", () => {
    expect(
      valueVsSpec({ value: 31.4, unit: "C", specLow: 28, specHigh: 30, excerpt: "" }),
    ).toBe("31.4 °C, recipe spec 28-30 °C");
    expect(valueVsSpec({ excerpt: "The latest reading is 6.52 Torr, outside the 2–6 Torr window." })).toBe(
      "6.52 Torr, recipe spec 2-6 Torr",
    );
    expect(valueVsSpec({ excerpt: "Calibration is current." })).toBe("—");
    expect(formatMeasure(31.4)).toBe("31.4");
    expect(formatMeasure(2)).toBe("2");
  });

  it("drops record ids, raw timestamps, and guids from the sentence an operator reads", () => {
    const raw = `ECO-4471 on E-02 at 2026-10-14 21:42 UTC record ${GUID}.`;
    expect(plainNarrative(raw)).not.toMatch(/ECO-4471/);
    expect(plainNarrative(raw)).not.toMatch(/2026-10-14/);
    expect(plainNarrative(raw)).not.toMatch(new RegExp(GUID, "i"));
    expect(plainNarrative(raw)).toContain("Etch tool E-02");
    expect(oneSentence(raw).length).toBeLessThanOrEqual(160);
  });

  it("rewrites an inline timestamp and prefers the structured place and parameter", () => {
    expect(readableExcerpt("effective 2026-10-14 21:42 UTC on the tool", "UTC")).toBe(
      "effective Oct 14, 9:42 PM UTC on the tool",
    );
    const reading = item({
      title: "westernElectric1 chamberPressure",
      excerpt: "SPC rule broke on D-01 at 2026-10-14T21:42:00Z.",
      toolId: "D-01",
      toolName: "Deposition tool D-01",
      chamberName: "Chamber A",
      signalId: "chamberPressure",
      value: 6.52,
      unit: "Torr",
      specLow: 2,
      specHigh: 6,
    });
    expect(whatItShows(reading)).toBe("Chamber pressure on Deposition tool D-01, Chamber A");
    expect(valueVsSpec(reading)).toBe("6.52 Torr, recipe spec 2-6 Torr");
  });

  it("labels a step duration without a raw status code", () => {
    expect(formatStepDuration("queued", 4)).toBe("Not started");
    expect(formatStepDuration("running", 4)).toBe("Running");
    expect(formatStepDuration("done", 12)).toBe("12 s");
    expect(formatStepDuration("done", 1)).toBe("1 s");
    expect(formatStepDuration("escalated", 2)).toBe("Escalated · 2 s");
  });
});

describe("record ids stay raw", () => {
  const ids = ["RUN-BG-20261014-E-02-W05-03", "LOT-E-02-W05", "LOT-C-01-W02", "NCR-S-01-052", "I-D-01", "WO-PMP-D02-BRG", "TKT-1001"];

  it("only standalone tool ids in prose get the friendly name", () => {
    const text = "MES run RUN-BG-20261014-E-02-W05-03 on E-02 started: recipe OXIDE-ETCH revision B, lot LOT-E-02-W05. Chamber E-02-A held.";
    const out = plainFinding(text);
    expect(out).toContain("RUN-BG-20261014-E-02-W05-03");
    expect(out).toContain("LOT-E-02-W05");
    expect(out).toContain("on Etch tool E-02 started");
    expect(out).toContain("Etch tool E-02-A");
    expect(readableExcerpt(text)).toContain("RUN-BG-20261014-E-02-W05-03");
    expect(readableExcerpt(text)).toContain("LOT-E-02-W05");
    expect(plainNarrative(text)).toContain("on Etch tool E-02 started");
    expect(plainNarrative(text)).not.toMatch(/-(Etch|Deposition|Strip|Clean) tool /);
  });

  it("never rewrites the inside of any record id", () => {
    for (const id of ids) {
      for (const format of [plainFinding, readableExcerpt]) {
        expect(format(`Record ${id} was checked.`)).toContain(id);
      }
      expect(plainNarrative(`Record ${id} was checked.`)).not.toMatch(/(Etch|Deposition|Strip|Clean) tool/);
    }
  });
});
