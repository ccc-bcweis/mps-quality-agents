# GB-DOCC report

Lane DOC-C. Local commit only. No git network commands.

## Files changed

- `data/glossary.yaml` (values updated on purpose)
- YAML comments only: `data/commands/catalog.yaml`, `data/evidence-matrix.yaml`, `data/fleet/mps-c7.yaml`, `data/fleet/mps-d7.yaml`, `data/fleet/mps-e7.yaml`, `data/fleet/mps-s7.yaml`, `data/metadata/category-evidence.yaml`, `data/metadata/decision.yaml`, `data/metadata/family-envelopes.yaml`, `data/metadata/investigation.yaml`, `data/metadata/simulation.yaml`, `data/metadata/spc-rules.yaml`, `data/metadata/tools/clean.yaml`, `data/metadata/tools/deposition.yaml`, `data/metadata/tools/etch.yaml`, `data/metadata/tools/metrology.yaml`, `data/metadata/tools/strip.yaml`
- Schema `description` strings only: `contracts/metadata/business-rules.schema.json`, `capabilities.schema.json`, `company.schema.json`, `lines.schema.json`, `sites.schema.json`, `tools.schema.json`
- `docs/architecture/data-model.md`
- `data/kpis/README.md`, `data/replay/README.md`, `data/replay/scripted/README.md`, `data/seed/README.md`
- `design/wireframes/README.md`
- this report

No YAML key, value, or list item changed except glossary text. No schema property, type, enum, required list, or pattern changed.

## What the comments and docs now say

Recipe steps each define every parameter. An idle parameter still has a window. The fleet load check refuses a step that leaves one out. Nothing falls back to another step. Control limits are the recipe window's center plus or minus three standard deviations, clamped to the sensor's physical floor and ceiling. The band keeps following the recipe step during a fault. Catalog `lcl`, `ucl`, `alertLow`, `alertHigh`, and `nominal` are family-envelope bounds, not the chart limits.

`telemetryWindow.readingsPerSensor` (600) is the rolling window in memory and in the database, and the chart's fixed x-axis width (`windowPoints`). A reset primes that window in memory and does not put a week of history on the bus. A hard reset clears the telemetry table. A soft reset keeps the stored readings.

`recipeSetpointEditWithoutEco` maps to process drift. Supplier notices are returned only for material lots that tool used.

Shape wait starts at incident open time, or the first serious rule break when there is no incident time. CauseGuard treats a mention and a citation differently. A citation miss is not retried. A shape or category miss is retried `shapeRetries` times, then the case escalates and the refused answer is saved as `RejectedAnswerJson` (`rejectedAnswer` on the API while escalated). The round number is `Investigations.Sequence`. Evidence rounds stop at `evidenceRounds.max` (5). Linked records are the tool's tickets, alerts, investigations, and manual readings. A Started placeholder is replaced when the saved round arrives.

Model mode is `Mps:Model:Mode` (`MODEL_MODE`): offline, openai, or azure. `LLM_MODE` is an old alias. A failed live call is not retried by the client. The gateway answers from saved answers. The plant recording (`data/replay/SC-*/messages.jsonl`) does not choose the model. A missing scripted hash is not answered from that folder.

## Stale comments and sentences removed

- Shape wait timed as "20 readings at 15 s = 5 minutes" and "180 readings = 3 minutes at 10x; at 1x the attempts run out first." Onset is no longer "the first rule break" alone.
- Simulation header that said the generator settings shape the data and not the rules it is judged by. Rules are scored on the recipe-step window.
- Replay README: "Replay mode in the console reads these files. Live mode ignores them." That plant view is separate from model mode.
- Scripted README: a missing hash "falls through to the live route," and "Live wording may still vary."
- Wireframes README: the harness "mode line (live / replay) with today's spend" as the running console, the tool page "Windows from SPEC §2.1," and the spend path "$1.24 of $2.00 in live, $0.00 in replay." The static HTML still draws the old switch. The running console uses the model-mode chip.
- KPI README: "The live sensor limits in the fleet files are a separate spec band."
- Data model: "Those tables are not created here" for the event log and telemetry, "A process map is either current or future," evidence and approval as child tables, and a telemetry parameter's lower and upper limits as the chart limits. Business rules stay: first match wins, lowest order first.

## Glossary

Eight terms added, in the existing four-field one-line shape: recipe window, idle window, physical range, telemetry window, model mode, rejected answer, process drift, linked records. Updated in place: recipe step, control limits, ECO, supplier notice, AOAI, MEAI. OIDC was already present and was left as it was. No required term was removed. 205 terms, no duplicates.

## Verification

Comment-only YAML, 17 files, `yaml.safe_load` then `json.dumps(..., sort_keys=True)` against `git show HEAD:path`: identical.

`data/glossary.yaml` parsed output differs, as intended (new and updated terms only).

Six schema files: every JSON difference is a `description` string. Required lists, enums, types, and patterns match HEAD.

`dotnet test tests/Simulator.Tests/Simulator.Tests.csproj --filter FullyQualifiedName~GlossaryTests`: Passed, 1 test.

`cd web; npm ci` then `.\node_modules\.bin\vitest.cmd run src/glossary` (vitest 3.2.7): 3 files, 12 tests passed. `web/node_modules` is not committed.
