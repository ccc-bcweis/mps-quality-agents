# Demo reproducibility

The pitch uses one seed so a reset on this computer and a reset in the hosted demo start from the same fleet.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## Seed

`Mps:Demo:Seed` defaults to 4242. Telemetry for a tool, chamber, parameter, and window is a function of that seed. `POST /api/admin/reset` takes `kind`. Omitted or `hard` wipes transactional data, keeps metadata, puts the simulated clock back at 2026-09-01T00:00:00Z, rebuilds the eight-tool fleet, and fills a week of healthy history. Ticket numbers start again at TKT-1001. Scale stays 60, so one real second is one simulated minute. `kind: soft` deletes nothing. It closes open tickets, investigations, and pending approvals with the status Closed by demo reset, clears live faults, and sends one healthy reading for every sensor. History stays browsable. The harness polls `GET /api/admin/reset/progress` while either reset runs.

The same seed produces the same scenario telemetry. Live model wording can still vary slightly. Every live call is pinned to temperature 0. The OpenAI seed parameter is set when the provider supports it (`Mps:Model:Seed`, falling back to `Mps:Demo:Seed`). Pinning reduces variation. It does not make every provider return the same sentences.

## Scripted scenarios

| Scenario | Class | What the presenter runs |
|---|---|---|
| 1 | Engineering change | Edge ring revision C on E-02. ESC temperature oscillates on chamber A. |
| 2 | Equipment out of spec | ESC thermocouple calibration drift on E-01. |
| 3 | Supply-chain material | A bad precursor gas lot. CF4 flow steps down on E-01 and E-02. |
| 4 | Process drift | Slow chamber seasoning. Etch rate ramps on E-01 chamber A. |

```
SC-1 engineering change
SC-2 equipment calibration
SC-3 supply chain, precursor lot PL-2044
SC-4 process drift
```

Trigger a scenario from the demo harness bar (the scenario picker sits above the console) or from `POST /api/injector/scenarios/{id}`. The free-form fault form is the harness fault-injector panel and still posts `POST /api/faults`. Unknown ids return 404. Which screens are the harness and which are the application: `docs/demo/SCREENS.md`.

The sample window is the first 30 seconds after 2026-09-01T00:00:00Z, so the series does not depend on when the button is pressed.

## Plant-data replay

Live is the running plant. Replay swaps the production line, the tool charts, and the alerts to a recorded scenario. Recordings are bus messages in `data/replay/<scenario>/messages.jsonl`, written by `tools/ReplayRecorder` or `POST /api/admin/replay/{scenarioId}`. Saved model answers stay in `data/replay/scripted/` and are not those files. `POST /api/demo/data-mode` with `live` puts the console back on the running plant and leaves the recording in place. The answer engine (saved answers or a live model) is a separate switch.

## Model replay

`Mps:Model:ScriptedReplay` defaults to false. When it is true, the gateway looks up a recorded turn by scenario id plus the prompt hash before it calls a live model. The hash covers the model id, the messages, and the tool names. It does not include temperature or the seed, so pinning does not change the key.

Recordings live in `data/replay/scripted/{scenarioId}/{promptHash}.json`. A missing directory is an empty catalog. A miss falls through to the live route. When `LLM_MODE` is `replay`, there is no live client, and a scripted miss uses the existing replay catalog and then the mock reasoner. `LLM_MODE` stays `replay` in `appsettings.json`, so startup does not need a key.

The scenario id is the chat option `mps.scenarioId`. While a named scenario is being applied, the API also sets that id on an ambient hint.

Live AI wording may vary slightly even with temperature 0 and a fixed seed. Turn scripted replay on when the pitch must speak the same sentences, and check a recording in for that scenario and prompt hash.
