// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
import { describe, expect, it } from "vitest";
import { upsertInvestigation } from "./stream";

const placeholder = { id: "tmp", incidentId: "n-1", round: 1, stage: "started" };
const saved = { id: "inv-1", incidentId: "n-1", round: 1, stage: "recommendationReady" };
const other = { id: "inv-9", incidentId: "n-9", round: 1, stage: "awaitingApproval" };

describe("upsertInvestigation (#378)", () => {
  it("replaces the started placeholder of the same round with the saved record, in place", () => {
    expect(upsertInvestigation([other, placeholder], saved)).toEqual([other, saved]);
  });

  it("never lets a late placeholder replace or sit next to the saved record", () => {
    const list = [saved, other];
    expect(upsertInvestigation(list, placeholder)).toBe(list);
  });

  it("updates a record by id and keeps other incidents and rounds", () => {
    const next = upsertInvestigation([saved, other], { ...saved, stage: "approved" });
    expect(next.map((item) => item.stage)).toEqual(["approved", "awaitingApproval"]);
    const round2 = { id: "tmp-2", incidentId: "n-1", round: 2, stage: "started" };
    expect(upsertInvestigation([saved], round2)).toEqual([round2, saved]);
  });

  it("treats a record without a round as round 1", () => {
    const legacy: { id: string; incidentId: string; round?: number; stage: string }[] = [{ id: "tmp", incidentId: "n-1", stage: "started" }];
    expect(upsertInvestigation(legacy, { ...saved, round: undefined })).toEqual([{ ...saved, round: undefined }]);
  });
});
