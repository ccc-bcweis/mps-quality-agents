# Current state and future state

**Prepared by StatusNeo for Meridian Precision Systems**
**Issue:** G5 (#74)
**Date:** 2026-10-14
**Copyright (c) 2026 Bryan C. Weis and Common Consulting Company**

This note is the consultative reading behind the plotter sheets. It describes how a quality investigation moves at Meridian today, and how the assisted investigation changes that path. Meridian Precision Systems is fictional. The MX-400 etch is a fictional etch tool used as the worked example. Every duration and every hand-off count below is simulated.

The sheets:

- Current process, full swimlane: `docs/diagrams/print/as-is-flow-archD.pdf`
- To-be process, same lanes plus the agent platform (G6): `docs/diagrams/print/to-be-flow-archD.pdf`
- Assisted process, screen order (G4): `docs/diagrams/print/investigation-flow-archD.pdf`
- Side by side, with benefits: `docs/diagrams/print/as-is-vs-to-be-archD.pdf`

## Current-state findings

An alarm on the MX-400 etch does not open a ticket. The operator notices a limit, notes the chamber and the recipe, and tells a technician in person. The first record is a spoken sentence.

The technician types a ticket into email. The quality engineer assigns an owner by reply. From there, the evidence is collected by export. Five separate pulls feed one meeting:

- The technician exports the manufacturing run sheet (the manufacturing execution system) as a spreadsheet.
- The process engineer copies historian traces, the statistical process control chart, and the engineering-change log. The change log is a separate scroll, checked by eye.
- The quality engineer pulls prior cases from a folder and forwards them into the thread.
- Supply chain looks up the supplier lot and emails it. The lot is not joined to the run.

The meeting compares those files. There is no shared evidence pack. The hypothesis is tribal knowledge: the last similar alarm was a dirty chamber, so this one is stated the same way. The four cause classes are not checked as a set.

Approval is a meeting or a mail reply. No evidence pack is attached. The quality engineer then re-keys the narrative, the lot, and the actions into the quality management system. The spreadsheets stay in the mail thread. Management signs the record and does not see the files.

We count **13 manual hand-offs** on that path, and **11 places a fact can change**. The count is the red badges on the as-is sheet. It is a reading of this fictional process, not a time study.

## Pain points and error risks

| Badge | Where it happens | What goes wrong |
|---|---|---|
| E1 | The operator tells the technician | Spoken alarm. The chamber is written from memory. |
| E2 | The technician types the ticket | Ticket retyped. Tool, time, and symptom change. |
| E3 | The quality engineer assigns an owner | Data gap. The reply drops the recipe step. |
| E4 | The run sheet is exported | The manufacturing export is pasted into a spreadsheet. |
| E5 | The change log is copied | Missed engineering change. The log is checked by eye. |
| E6 | Prior cases are forwarded | Data gap. The cases sit in a folder, not on the ticket. |
| E7 | Supply chain emails the lot | Wrong lot traced. The supplier lot is not joined to the run. |
| E8 | The meeting | Data gap. The call does not record which file was current. |
| E9 | The hypothesis | Bias toward the last known cause. |
| E10 | Management approves | Uncited approval. The meeting has no evidence pack. |
| E11 | The record is re-keyed | The quality system gets a second copy. The files stay in email. |

The pattern is the same at each step. A fact leaves one system, crosses a person, and enters another system or a thread. Transcription, a dropped field, a lot that does not join, and a cause chosen because it was true last time are the ordinary failure modes. The record that management signs is the last copy, not the evidence.

## Future state

The assisted path drawn on the same lanes as the current process is the G6 sheet. The G4 sheet is the same path in screen order. Statistical process control opens the ticket from the rules. Nobody retypes the alarm. The engineer opens the ticket. An investigation agent plans the evidence pack and does not name a cause. Read-only calls bring back the manufacturing, quality, product-lifecycle, planning, and maintenance records, plus the documents, as one pack. Each evidence item carries one plain sentence on why it matters.

Four cause classes are checked on every incident: an engineering change, equipment out of spec, supply-chain material, and process drift. A resolution agent ranks one cause and names the signal that rules a class out. A grounding check rejects a claim that cites nothing in the pack. The engineer approves or rejects with a signed token. Only then does the action service write the corrective and preventive action. The agent draft is not a write.

If the evidence is missing or contradictory, the evidence gate stops the path before a cause is ranked. The engineer owns the named gaps. No root cause is asserted, and the resolution agent is not called. That branch is drawn on the G4 sheet. The side-by-side sheet shows the path where the evidence is sufficient.

A later check can still refuse an answer the model did give, after the gate has passed. The cause guard allows a ruled-out record to be mentioned when the text is dismissing it. It escalates when that record is cited as support, or named as the cause of its own class. The refused answer is kept and shown under the evidence gate as "What the AI proposed (not accepted)". It is not the recommendation.

On that sufficient path we count **2 human hand-offs**: the engineer opens the ticket, and the engineer approves. The other movement is rules and read-only calls.

## Stage-by-stage comparison

Durations are elapsed time for one simulated incident. Hand-off counts are the manual steps on the as-is sheet and the human steps on the to-be sheet.

| Stage | As-is duration | To-be duration | As-is manual hand-offs | To-be human hand-offs |
|---|---:|---:|---:|---:|
| Detect | 0.5 day | 0.1 day | 1 | 0 |
| Triage | 0.5 day | 0.2 day | 2 | 1 |
| Gather evidence | 2.0 days | 0.4 day | 5 | 0 |
| Diagnose | 1.0 day | 0.5 day | 1 | 0 |
| Decide / approve | 2.0 days | 2.0 days | 2 | 1 |
| CAPA, the record cycle | 62 days | 40.3 days | 2 | 0 |
| Detect through Diagnose | 4.0 days | 1.2 days | 9 | 1 |
| All stages |  |  | 13 | 2 |

Detect through Diagnose is the simulated time to a root-cause hypothesis. The CAPA row is a different clock: the corrective-and-preventive-action record cycle. Decide / approve is shown at 2.0 days on both sides.

## Benefits

**Time to root cause.** 4.0 days down to 1.2 days, simulated. That reduction sits in Detect through Diagnose. The gather stage is most of it: 2.0 days of exports and a meeting, against 0.4 day of read-only calls into one pack.

**Fewer hand-offs.** 13 manual hand-offs down to 2, simulated. The two that remain are the engineer opening the ticket and the engineer approving. The retypes, the folder, the lot email, and the re-key into the quality system come out of the path.

**A traceable evidence trail.** The as-is record is a mail thread plus a re-keyed narrative. The to-be record is one pack. Each cited claim points at an item in that pack, and a claim that points at nothing is rejected before the engineer sees it.

**A consistent four-cause check.** Today the hypothesis follows the last known cause. In the assisted path the same four classes are named every time, and the rank has to say what would rule a class out.

**The human stays the approver.** A signed token is required before the record is written. Agents do not write. The person who decides is the same person who decides today. The change is what that person is asked to sign: a grounded recommendation, not a thread.

## Assumptions

The totals are the simulated baseline and the expected case in PLAN section 6.5 and `docs/roi-assumptions.md`.

- Time to a root-cause hypothesis is 4.0 days in the baseline. The expected case reduces that interval by 70 percent, which is 1.2 days. The split inside the 4.0 days (0.5, 0.5, 2.0, 1.0) and inside the 1.2 days (0.1, 0.2, 0.4, 0.5) is illustrative. The totals are the plan. The split is ours, so the stage bands can be compared.
- The corrective-and-preventive-action cycle is 62 days in the baseline. The expected case reduces that cycle by 35 percent, which is 40.3 days. That lever is separate from the hypothesis clock.
- Mean time to resolve a quality incident is 9.5 days in the baseline. The hypothesis interval sits inside it. The ROI model leaves the other 5.5 days unchanged, so the expected mean time is 6.70 days. Decide / approve is drawn as 2.0 days of those 5.5 on both sides. We do not claim the approval itself gets faster.
- Engineer labor is 22 hours per investigation in the baseline. That is labor, not elapsed time. It is not added to the 4.0 days.
- The 13 and the 2 are counts of steps on the two sheets for the sufficient-evidence path. The escalation path adds a human owner for the gap and does not add a ranked cause.
- SPEC section 1 describes a pitch range of 5 to 15 days to a hypothesis, and four roles (quality engineer, process engineer, maintenance, quality manager). This note uses the six roles on the as-is sheet, so the hand-offs are visible, and it uses the 4.0-day worked baseline so the comparison matches the ROI model.
- SPEC also treats "about 40 to 60 percent of investigation time spent gathering" as an assumption still to be validated. The gather band is where that time sits in this reading. We have not split the 22 labor hours across stages.

## Caveats

These figures are simulated. They are not a measurement from a Meridian plant, and they are not a forecast of savings. A real baseline would be a short measurement of the current path, then a shadow period in which the assisted pack is compared with the path people already run.

The stage split can move without moving the totals. If a plant's gather step is three days and its diagnosis is half a day, the 4.0-day total can still hold while the bands change. The bands are a teaching picture.

The CAPA cycle shrinks in the ROI model because the record is started from a grounded pack. This note does not claim that containment, verification, or the rest of the 62 days disappear. 40.3 days is the simulated expected cycle, still a long record.

The human approval is the control, not a step to remove. A deployment that let the agent write the quality record would be a different proposal. This one does not.
