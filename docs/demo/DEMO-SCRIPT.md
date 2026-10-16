# Demo script: Meridian Precision Systems quality agents

Prepared by StatusNeo. About 20 minutes plus questions. Every company, plant, tool and record is fictional.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## The three brief scenarios

| Brief | In this script | Reproduce |
|---|---|---|
| S1: a part change drives a drift | Section 3, SC-1 (ECO-4471 on E-02) | Reset, scenario picker SC-1, Run scenario |
| S2: a manual bench reading is out of spec | Section 5 (thermocouple printout on E-01) | Tickets > Upload telemetry, `data/samples/thermocouple-logger-e01.png` |
| S3: one supplier lot hits two plants | Section 6, SC-5 (lot MX4419) | Reset, scenario picker SC-5, Run scenario |

Seed 4242 makes each run the same; see `REPRODUCIBILITY.md`.

## Before you start (5 minutes before)

1. Open the hosted app and sign in. Check the harness bar shows **AI engine: Replay**.
2. Press **Reset demo** in the harness bar and confirm. Every tool on the Production line is green (Nominal). Alerts, Tickets, Investigations and CAPA lists are empty.
3. In the site switcher in the console header, choose **All sites**.
4. Have `data/samples/thermocouple-logger-e01.png` ready on the desktop for the upload moment.
5. Decide the AI engine. Replay gives saved answers: free and the same every time. Live calls the real model (a few cents per run, capped at $2 a day). Neither changes the plant data.

## Two layers on screen

- **StatusNeo demo harness** (navy and gold bar at the top): the presenter's tools. Scenario picker, fault injector, Reset demo, return on investment, metrics, audit log, AI engine.
- **Meridian console** (charcoal, below): what Meridian's engineers would use. Sites, Production line, Tool, Alerts, Tickets, Investigation, Approval, CAPA.

Say it once: "The bar at the top is our demo control panel. Everything below it is the product."

## 1. Opening: who Meridian is (2 min, deck slides 1-3)

- Company overview and site map: three plants, Northwater Fab, Ironwood Fab and Lumen Pilot Works.
- The problem: when a tool drifts, engineers spend hours pulling data from five systems before anyone can name a cause.

## 2. Sites and the production line (3 min)

1. Console > **Sites**. Show the three plant tabs, the plant throughput strip and the floor blueprint. Point out bays, tools and the numbered wafer flows.
2. Click **Ironwood Fab**. The header site switcher changes to Ironwood; Production line, Alerts and Tickets now show only Ironwood. Switch back to **All sites**.
3. Scroll to **Cross-plant comparison**. Same tool family at each plant, readings that differ listed first.

Talking point: "One view across plants, so a problem at one fab can be checked against the others in seconds."

## 3. Scenario 1: a part change on an etch tool (5 min)

1. Harness > scenario picker > **SC-1 Edge ring revision C on E-02**. Read the card's *What's happening*: the edge ring is the ring around the wafer in the etch chamber; a new revision changes how plasma reaches the wafer edge.
2. Press **Run scenario**. Do not click anything for about a minute. Watch:
   - Production line: E-02 turns amber, then red. The tile, the Sites map, and the tool page always show the same status (the worst active alert).
   - Click **E-02**: the schematic shows Chamber A, the electrostatic chuck temperature starts to oscillate, and the SPC chart crosses its limit.
3. **Alerts**: one row per active alert (status, tool, time, rule, ticket, Open). The count matches the Production line. Press **Open** to see that alert's evidence. **Tickets**: a ticket opened automatically.
4. **Investigation** opens the run. Walk the evidence table top to bottom: source, what it shows, value against spec, when, why it matters. Expand one row for details.
5. Show the **agent trace** timeline: each step with a one-line summary and how long it took. Mention the raw payloads are behind Details, and IDs live in the audit log.
6. The ranked cause: **Engineering change, ECO-4471 edge ring revision C**, with the maintenance the same week shown as a confounder that was ruled out.

Talking point: "Minutes, not a day. And it shows its work: every claim points at a record."

## 4. Human in charge: approval and CAPA (3 min)

1. **Approval**: approve with a comment. The screen shows "Approved by Bryan Weis at 10:42 PM", not tokens or hashes.
2. **CAPA**: the corrective action record is created from the approval. The agent cannot write it on its own.
3. Escalation gate (ADR-005): if an investigation escalates, Approve is not offered. The engineer chooses **Request more evidence** (re-runs) or **Override with justification** (a written reason of at least 20 characters, recorded in the audit log).

## 5. Manual measurement upload (2 min)

1. **Tickets** > **Upload telemetry**. Drop `thermocouple-logger-e01.png`, a scan from a bench thermocouple logger.
2. The app reads it: tool E-01, Chamber A, ESC temperature 68 C against the printed spec of 40-60 C. The printed time is days old, so press the gold **Now** button. The printed time stays on the record for audit. Press **Confirm**.
3. The reading goes in as one ordinary sample tagged Manual, in the same series as the sensor. The normal rules decide what happens next. One point above 3 sigma trips Western Electric rule 1, so E-01 goes to alarm and a ticket opens. The investigation then starts on its own. The tile shows 68 C with a Manual badge, the chart shows the marker, and the Why panel names the printout.
4. To show the contrast, an in-spec reading (for example 50 C) is stored and charted but opens no ticket.
5. After containment, or after 8 normal readings, the alert clears. The tile goes back to the live sensor, and the marker is labeled "resolved". The printout stays as history in the evidence and the audit log.
6. In **Operator report**, choose a tool. The parameter list shows only that tool's real sensors, with units and spec ranges.

## 6. A cross-plant problem (2 min, optional)

1. Reset demo. Run **SC-5 Precursor lot across two plants**.
2. Deposition tools at Northwater and Ironwood both step down in film thickness.
3. The ranked cause is supply chain: lot MX4419 was used at both plants, and the supplier notice says it is out of spec. A chamber clean at one plant is ruled out because it cannot explain the other plant.

Other scenarios if time allows: SC-2 overdue thermocouple calibration, SC-3 bad gas lot, SC-4 slow seasoning drift, SC-7 pump bearing wear, SC-8 operator recipe change.

## 7. Value and close (3 min, deck)

- Harness > **Return on investment**: illustrative, synthetic numbers.
- Harness > **Metrics**: category accuracy from the replay eval (24 cases, run in CI), and cost per successful investigation (n/a until one succeeds).
- Architecture slide, security slide, rollout plan, closing.

## If something goes wrong

- Nothing moves after Run scenario: wait 30 seconds (the stream ramps in), then check the AI engine and press Reset demo and run again.
- Live model is slow or over the cap: switch AI engine to Replay. Plant data does not change.
- A tab looks empty: that list has no records yet for the selected site; switch to All sites.
