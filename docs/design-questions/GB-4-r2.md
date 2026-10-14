# Design questions (GB-4 round 2)

The console builds against the frozen contracts. None of these block the demo. Contract files and `data/` were not changed. ADR-003 says the data lane owns `data/glossary.yaml` and the sensor catalog, and that agent output carries `whyItMatters`. This lane rendered what it could and recorded the gaps here.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## 1. `data/glossary.yaml` is not in this checkout

Context: UX1 and ADR-003 name one shared glossary. The file is absent. Vite loads `web/src/glossary.fallback.json` and the Glossary page shows that note. The acronym test reads the same module the app uses, so a present YAML file becomes the source the test checks.

Options: The data lane adds `data/glossary.yaml` with a `terms:` list of `term`, `expansion`, `definition`, and optional `whyItMatters`. The UI keeps the stub until that file exists.

Recommendation: Add the YAML in the data lane. The UI parser already prefers it and clears the note when the file is present. Folded scalars and nested maps are outside the parser.

Blocking: no.

## 2. Fleet YAML has no `sensors:` list and no `schematicAnchor`

Context: UX2 binds schematic points to `data/fleet/*.yaml` sensors (`id`, `type`, `unit`, `subsystem`, `schematicAnchor`). Those files have tools and recipes and no root `sensors:` list. The build then uses `web/src/fleet/sensors.fallback.json`, including the live `value`, `display`, and `status` the buttons show. If any fleet file parses to one or more sensors, the stub is dropped and only the YAML sensors are used.

Options: The data lane adds a `sensors:` list per family file. The UI keeps the stub and the parser.

Recommendation: Add the catalog in the data lane. A sensor with no `toolId` is copied onto each id under that file's `tools:` list, and its id becomes `{id}:{toolId}`. The parser accepts a flat block list or a one-line flow mapping. It does not accept folded scalars. Put `value`, `display`, and `status` in that catalog when the buttons should leave the stub. An anchor with no matching point is listed as "not on the drawing" (E-02 `endpoint.time` is the demo of that).

Blocking: no.

## 3. Evidence items have no `whyItMatters` in the OpenAPI schema

Context: SPEC §2.10 and ADR-003 require a why-it-matters line on each cited evidence item, and they say the grounding validator checks it. `EvidenceItem` in `contracts/openapi.yaml` has id, source, title, excerpt, observedAt, toolId, and externalRef. The UI added an optional `whyItMatters` on the local TypeScript type and filled it in the demo pack. The investigation page prints it. The frozen contract was not edited.

Options: Add `whyItMatters` to the evidence schema and have the grounding validator require a real evidence id plus that line. Keep the optional local field until the contract moves.

Recommendation: Add the field on the agent-output contract in the agents lane, then drop the "local demo field" comment. Until then the demo line is present only when the JSON includes it.

Blocking: no.

## 4. The root-cause evidence matrix is not an API resource

Context: The matrix (supports / rules out / open, across engineering change, equipment, material, and process drift) is a local overlay in `web/src/investigation/matrix.ts`, keyed by the S1 and S2 investigation ids. It is not derived from evidence ids, and it is not in the OpenAPI file.

Options: Have the agent pack publish the matrix. Keep the two demo grids in the web app.

Recommendation: Keep the demo grids for the pitch. A later contract can replace `matrixFor` without changing the table.

Blocking: no.

## 5. Run rate, uptime, OEE, yield, MTBF, and MTTR are not on the API

Context: UX4 asks for those figures per tool and for the fleet. No tool or fleet schema carries them. `web/src/fleet/performance.ts` holds illustrative numbers. The fleet row is the arithmetic mean of the eight tools. The screen says "Illustrative demo readings, not a live tool counter."

Options: Add a read-only performance block to the tool resource, or publish it beside the sensor catalog. Keep the client table until then.

Recommendation: Keep the labeled client table for the pitch. The Metrics page stays the gateway eval screen (hard gate, blank scores) and does not show these fab figures.

Blocking: no.

## 6. The agent timeline is derived from `investigation.stage`

Context: The timeline is "live" in the sense that the page polls `GET /api/investigations/{id}` every 5 seconds (`INVESTIGATION_POLL_MS`) and maps `stage` onto the eleven steps. SignalR `/hubs/fleet` has no step payload. There is no workflow event stream in the frozen contract. The page does not autoplay a script.

Options: Publish step status on an investigation or hub payload. Keep the stage map.

Recommendation: Keep the stage map for the demo. S1 (`recommendationReady`) shows the work through grounding as done and human approval as queued. S2 (`escalated`) shows the evidence gate as escalated and ranking onward as queued.

Blocking: no.

## 7. The clean schematic adds `bath.megasonic`

Context: Deposition, etch, strip, and clean share anchors where the hardware exists (handler, gas, RF, chamber, chuck, vacuum). The shared list has no megasonic point. The clean drawing places the megasonic power at `bath.megasonic` so C-01 can show 720 W on the bath.

Options: Add `bath.megasonic` to the shared anchor vocabulary in the sensor catalog. Drop the megasonic point from the clean drawing.

Recommendation: Add `bath.megasonic` to the catalog when the data lane writes `sensors:`. The clean drawing already has the point.

Blocking: no.

## 8. The container image does not build the Vite app

Context: `deploy/web.Dockerfile` still copies `deploy/web/index.html` and comments that there is no `web/package.json`. The console now lives in `web/` and CI builds it. This lane was limited to `web/` and the CI web job, so the Dockerfile was left in place.

Options: Change the image to build `web/` and serve `web/dist`. Leave the placeholder until the deploy lane picks it up.

Recommendation: Have the deploy lane build the Vite app in that image. CI already typechecks, lints, tests, and builds `web/`.

Blocking: no.
