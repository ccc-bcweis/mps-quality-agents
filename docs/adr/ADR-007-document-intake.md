# ADR-007: Bench printout intake

> Partly superseded by ADR-013 (2026-10-16): confirming a printout no longer injects a fault or opens its own ticket. The reading is one manual sample on the normal ingest path.

Status: Accepted for this demo build (issue #110, recorded by the document-intake lane) — 2026-10-15
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Context
An engineer sometimes measures a chamber with a bench instrument and comes back with a printout. That sheet is evidence. If the reading is outside the printed spec, the demo should also show the same fault on the simulated tool.

The printout is an operator action. ADR-006 keeps presenter controls (the fault injector, scripted scenarios, reset) in the harness band. A new tab or route would put a product feature in the wrong layer, or would add a console tab the rest of the pitch does not have.

The issue's example says an oscilloscope printout of RF oscillation on E-01 injects an oscillation on E-01 `rfPower`. The etch sensor catalog does not have a channel named `rfPower`. Source RF power on an etch tool is `rfSourcePower` (sensor spec 1000-2000 W). Deposition tools do use `rfPower` (sensor spec 300-600 W).

## Decision
Document intake lives on the Tickets tab. The panel is labeled "Upload telemetry" and sits beside the existing "Operator report" panel. There is no new tab and no new route.

The operator report parameter list, after a tool is chosen, shows only that tool's sensor channels from `data/fleet/*.yaml` (the same catalog the console loads as `virtual:fleet-sensors`). Each option uses a plain name, the unit, and the sensor spec range. The etch ESC temperature channel is spec 40-60 C. That is the sensor spec, not the recipe window 46-54.

Uploading a PDF or a PNG inspects it immediately. The engineer sees the extracted fields and can correct them, then confirms.

**Read order.** Text is extracted first with PdfPig. A scan, or a PDF with no text layer, goes to the model through the existing ModelGateway. That path keeps the daily spend cap and the mock, replay, and live modes. Mock mode does not invent a reading. It returns the reading stored for that file's sha256 in `data/samples/manifest.json`. An unknown hash is an error, not an investigation tool call.

**Fields.** Tool, chamber, timestamp, instrument, parameter, value, unit, and the printed spec limits. Shape and operator initials are kept when the sheet has them. Instruments are the oscilloscope, the RF power meter, the particle counter, and the thermocouple logger.

**Confirm.** The reading is stored as evidence with source `manualMeasurement`, linked to the tool and to the incident, so an investigation can cite it. The incident summary starts with "Manual measurement". Severity is high when the value is outside the printed spec, and low when it is inside. An in-spec reading stores the evidence and opens the low-severity incident. It does not inject a fault, raise an alert, or move the simulation.

An out-of-spec reading also calls `IFaultInjectionService` and applies the signal through the simulation port (`DemoRuntime.TryApplyFreeForm` in the API). This lane does not own the simulator tick. The alert rule is `manualMeasurement`. The fault's root-cause category is equipment calibration or alignment. The shape is the one printed on the sheet, or a step when the sheet does not name one. Magnitude is the sigma of the printed window: six sigma spans the printed minimum to the printed maximum. A reading that only just leaves that window is still injected at 3.5 sigma so the trace is visible.

**Etch `rfPower`.** The printed parameter is kept as the engineer saw it. On confirm, `rfPower` on a tool whose id starts with `E-` or contains `-E-` (E-01 and Ironwood I-E-01) is applied as `rfSourcePower`. Deposition, including D-01 and I-D-01, stays `rfPower`. The issue's "E-01 rfPower" example is that etch source-RF channel.

**Samples and SC-6.** `tools/Printouts` writes four synthetic PDFs, one per instrument, and a PNG scan of the thermocouple sheet. Makers are fictional. SC-6 is "Oscilloscope RF oscillation on E-01". Running it inspects and confirms `data/samples/oscilloscope-e01.pdf`. It does not also apply the catalog signal, so the tool shows one oscillation.

The injector's direct-ticket switch stays off. Confirm opens the incident itself. It does not call the SPC alert service, which would open a second incident.

## Rationale
Text extraction is repeatable and does not spend the model budget. The model is reserved for a sheet that has no text. Keying the mock answer on the file hash keeps CI and the demo deterministic without a live key.

The sensor spec, not the recipe window, is what a bench sheet is checked against. Showing another tool's channels in the operator report would let an engineer file a deposition parameter against an etch tool.

Mapping the printed word `rfPower` only for etch tools keeps the printout honest and gives the simulator a parameter it actually has.

## Consequences
- Contract 1.3.0 adds `POST /api/printouts/inspect` (multipart field `file`) and `POST /api/printouts/confirm`. Both are additive.
- Evidence source `manualMeasurement` is the last evidence-source value. Investigations for that tool append these measurements.
- The measurement log is in memory and is cleared on demo reset. Nothing is written under `data/` at runtime.
- PdfPig stays in Connectors. The model call stays in the API, on `IChatClient`. No new agent tool was added.
- The console copy uses PDF, PNG, RF, and ESC, which are already glossary terms.
