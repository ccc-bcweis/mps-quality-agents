# Demo reproducibility

The pitch uses one seed so a reset on this computer and a reset in the hosted demo start from the same fleet.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## Seed

`Mps:Demo:Seed` defaults to 4242. Telemetry for a tool, chamber, parameter, and window is a function of that seed. The same seed always rebuilds the same fleet, history, and scenario shapes.

## Clock

The plant runs in real time (`Mps:Simulation:RealTime` is true). The plant clock is the wall clock in UTC, and every reading is stamped with the time it was made. The seed and scenario records were written against 2026-09-01. On start, every date in them moves forward by whole days so that 2026-09-01 lands on today's UTC date. The gaps between records stay the same.

**Speed** (1x, 2x, 5x, 10x) does not change the clock. It makes an injected fault develop faster and shortens the gap between kept readings (the sample gap divided by the speed, at least one second). At 1x a fault develops 6 times faster than it was written, so a two-hour ramp takes 20 minutes. At 10x it develops 60 times faster, so the same ramp takes 2 minutes.

## Resets

`POST /api/admin/reset` takes `kind`. The harness polls `GET /api/admin/reset/progress` while either reset runs.

- Omitted or `hard` wipes transactional data and keeps metadata. It rebuilds the fleet, fills a week of healthy history that ends now, and sets speed to 1x with a 15-second sample gap. Ticket numbers start again at TKT-1001. The response reports `simulatedOrigin`, the start of today in UTC.
- `soft` deletes nothing. It closes open tickets, investigations, and pending approvals with the status Closed by demo reset, clears live faults, and sends one healthy reading for every sensor. History stays browsable.

The same seed produces the same scenario telemetry. Live model wording can still vary slightly. Every live call is pinned to temperature 0. The OpenAI seed parameter is set when the provider supports it (`Mps:Model:Seed`, falling back to `Mps:Demo:Seed`). Pinning reduces variation. It does not make every provider return the same sentences.

## Scripted scenarios

| Scenario | Class | What the presenter runs | Time to develop at 1x / 10x |
|---|---|---|---|
| SC-1 | Engineering change | Edge ring revision C on E-02. Chuck temperature oscillates on chamber A. | One swing about 3 min 20 s / 20 s |
| SC-2 | Equipment calibration | Overdue chuck thermocouple calibration on E-01. Chuck temperature ramps high. | Ramp 20 min / 2 min |
| SC-3 | Supply-chain material | Bad CF4 gas lot PL-2044. Flow steps down on E-01 and E-02. | Immediate step |
| SC-4 | Process drift | Slow chamber seasoning after a clean. Etch rate ramps down on E-01 chamber A. | Ramp 20 min / 2 min |
| SC-5 | Supply-chain material | Precursor lot MX4419 at Northwater D-03 and Ironwood I-D-01. Film thickness steps down. | Immediate step |
| SC-6 | Equipment calibration | Oscilloscope printout of RF source power oscillating on E-01. | Starts on confirm |
| SC-7 | Equipment calibration | Foreline pump bearing past its service interval on D-02. Vibration ramps up. | Ramp 25 min / 2 min 30 s |
| SC-8 | Process drift | Operator edited the RF bias setpoint on E-03 without an engineering change order. | Immediate step |

An alert fires when the rules see it, usually before a ramp finishes. Trigger a scenario from the demo harness bar (the scenario picker sits above the console) or from `POST /api/injector/scenarios/{id}`. The free-form fault form is the harness fault-injector panel and still posts `POST /api/faults`. Unknown ids return 404. Which screens are the harness and which are the application: `docs/demo/SCREENS.md`.

A scenario starts at the moment it is run. Its records keep their spacing from the scenario onset, so the evidence reads the same on every run.

## Plant-data replay

Live is the running plant. Replay swaps the production line, the tool charts, and the alerts to a recorded scenario. Recordings are bus messages in `data/replay/<scenario>/messages.jsonl`, written by `tools/ReplayRecorder` or `POST /api/admin/replay/{scenarioId}`. Saved model answers stay in `data/replay/scripted/` and are not those files. `POST /api/demo/data-mode` with `live` puts the console back on the running plant and leaves the recording in place. The answer engine (saved answers or a live model) is separate: it is set at start by `MODEL_MODE` and shown in the console's status chip.

## Model replay

`Mps:Model:ScriptedReplay` defaults to false. When it is true, the gateway looks up a recorded turn by scenario id plus the prompt hash before it calls a live model. The hash covers the model id, the messages, and the tool names. It does not include temperature or the seed, so pinning does not change the key.

Recordings live in `data/replay/scripted/{scenarioId}/{promptHash}.json`. A missing directory is an empty catalog. A miss falls through to the live route. In `offline` mode (`MODEL_MODE`, the default when no model key is set) there is no live client, and a scripted miss uses the replay catalog and then the rule-based reasoner, which ranks causes with the same `RootCauseRanker` the eval scores. `Mps:Model:Mode` is blank in `appsettings.json`, so startup does not need a key. Offline runs are repeatable: same seed, same evidence, same answer.

The scenario id is the chat option `mps.scenarioId`. While a named scenario is being applied, the API also sets that id on an ambient hint.

Live AI wording may vary slightly even with temperature 0 and a fixed seed. Turn scripted replay on when the pitch must speak the same sentences, and check a recording in for that scenario and prompt hash.
