# Lane report: GB-U1

Branch `lane/u1`. Local commits only. Nothing was pushed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

Issues #106, #109, and #111, plus the plant switcher and the AI engine toggle. ADR-005 (escalation gate) and ADR-006 (harness vs console) still hold. Investigation evidence and trace panels were left for lane EV. The scenario picker cards stay with lane SIM. The harness bar still has that slot.

## Done

| Piece | Commit | What |
|---|---|---|
| Console frame (#106) | `313e251` | The gold frame around the Meridian console is gone. The harness bar, console header, tabs, and footer stay put. Only the main pane scrolls. |
| Nominal start and reset (#109) | `7a60a66` | A fresh process has no faults, alerts, tickets, investigations, or corrective actions. Reset demo is a gold button on the harness bar. The confirm line is "Reset the demo? This clears faults, alerts, tickets, investigations and CAPAs." The console reloads after reset. The reset route stays admin-only. |
| Audit API (#111) | `c4bd7b6` | Read-only `GET /api/audit`. List routes take an optional `siteId`. A corrective action can carry who approved it and when. |
| Hide plumbing (#111) | `d5099bd` | Operator screens no longer show approval tokens, hashes, or correlation ids. An approval reads like "Approved by Bryan Weis at 10:42 PM" in local time. The harness has an Audit log link at `/harness/audit`. |
| Plant selection | `43dcbc3` | A site card, or the header plant list, stores the plant in React context and `localStorage` (`mps.selectedSite`). The list is All sites, Northwater Fab, Ironwood Fab, and Lumen Pilot Works. Fleet, Tool, Alerts, Tickets, Investigations, and corrective actions follow that plant. List calls pass `?siteId=` unless the choice is All sites. |
| AI engine | `6f5b556` | The harness toggle reads "AI engine: Replay (saved answers, free, repeatable) / Live (real model)". The hover tip says it does not change the plant data. The bar also says "Answers only. Plant data stays the same." The spend meter and the gateway note remain. |

## How it was verified

`dotnet build MPS.sln` succeeded with 0 warnings. `dotnet test MPS.sln` passed: 451 tests, 0 failed. `Api.IntegrationTests` matched none under the repo filter `Category!=PendingLane`. `dotnet format MPS.sln` made no further edits.

From `web/`: `npm test` passed (34 files, 87 tests). `npx tsc --noEmit` passed.

Headless Edge screenshots of the fleet shell at 1280, 1600, and 1920. The harness links, AI engine buttons, plant list, console tabs, and glossary link stay whole. They wrap onto the next line instead of clipping. Those shots ran with the mock worker off, so the scenario list and fleet data showed a server error. That error is the missing API, not the layout.

## Left for other lanes

- Lane SIM still adds the other plants' tools to the live fleet, and `siteId` on those records. Until then, Ironwood and Lumen show no tools on the live fleet. The client already filters by tool site id, and falls back to the `I-` and `P-` prefixes.
- Lane SIM still owns the scenario picker cards. The harness slot is unchanged.
- Lane EV still owns the investigation evidence and trace panels.

## Notes for Bob

- Reset does not clear the selected plant. Blocking: no.
- Opening Sites from a link such as `/sites?site=lumen` shows that floor and leaves the header on All sites until someone picks a plant. Blocking: no.
- Ironwood etch tools can still read Watch from plant behavior. That is not a seeded fault. Blocking: no.
- `GET /api/audit` is not admin-gated. Reset still is. Blocking: no.
