# Design questions (GB-4 round 4)

The console builds against the frozen contracts. This question does not block the demo. Contract files and `data/` were not changed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

## 1. OpenAPI has no tool telemetry snapshot

Context: UX2 (#60) shows a live value, unit, and spec-limit status on every sensor in `data/fleet`. `contracts/openapi.yaml` exposes `GET /api/tools` and `GET /api/tools/{toolId}` (id, name, family, model, bay, chambers). It has no sensor reading. SignalR `FleetHub` carries alert, ticket, incident, investigation, CAPA, and fault events, and it does not carry sensor frames. The simulator already emits the catalog sensor ids and units on its own telemetry frames.

The tool page reads a shared client generator (`web/src/fleet/telemetry.ts`) that uses those catalog ids, units, and spec limits. The same generator answers an MSW route `GET /api/tools/{toolId}/telemetry`. The page does not call that route, so a live API without the route does not 404 the Tool Detail screen. Two pitch samples are fixed: D-01 chamber A pressure is 6.52 Torr (above the spec maximum of 6), and E-02 chamber A ESC temperature is 53.1 C (inside the 42–58 warning band and the 40–60 spec). Other points are a deterministic sample inside the warning band. Reflected power follows the forward reading at the catalog nominal ratio.

Options:

1. Leave the contract as it is. The schematic keeps the shared generator until a later lane adds a snapshot or a hub frame.
2. Add `GET /api/tools/{toolId}/telemetry` to the OpenAPI file, with `sensorId`, `toolId`, `chamberId`, `parameter`, `unit`, `value`, and a status derived from the catalog limits. Point the page at that route and have the API serve the simulator's current frame.
3. Push the same fields on `FleetHub` and have the page subscribe.

Recommendation: Option 1 for this lane. The pitch already has ids, units, and spec-limit colors from `data/fleet`. Option 2 is the smaller contract when Bob wants the numbers to come from the API. Option 3 fits a ticking demo and needs a hub payload the contract does not have yet.

Blocking: no.
