# ADR-004: Multi-site simulation

Status: Proposed (lane S1, for Bob to accept)
Date: 2026-10-14
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

## Context

The demo started as one eight-tool line. The pitch needs three fictional plants so a supervisor can see the same tool family behave differently in two places, and so one material lot can be traced across plants. Lane S2 draws the Sites screens from the contract in this record. Lane R2 owns reset and the demo seed key. This lane must compose with that key and must not reuse scenario numbers S1 through S4.

## Decision

1. **Three fictional plants, and no real fab city.** Northwater Fab (`northwater`) is the front-end wafer fab. Ironwood Fab (`ironwood`) is a second front-end fab with the same tool families. Lumen Pilot Works (`lumen`) is strip, clean, and a pilot line. Place names are Cedar Reach in Valecrest, Ironwood Bench in Eastmere, and Lumen Yard in Westfold. Those names are invented. Time zones are ordinary zone names so clocks can be compared. They are not a claim that a chip plant sits in a real city. No real equipment vendor is named. Tool models stay the fictional MPS-D7, MPS-E7, MPS-S7, and MPS-C7.

2. **Original tool ids stay on Northwater.** `D-01`, `D-02`, `E-01`, `E-02`, `S-01`, `S-02`, `C-01`, and `C-02` keep their ids and their Bay-1 and Bay-2 assignments so the current console and the current tests keep working. Extra Northwater tools use the same id shape (`D-03`, `E-03`). Ironwood tools start with `I-`. Lumen tools start with `P-`. The prefix is the plant. `SiteIds.ForTool` is the rule that stamps enterprise records, so a tool id always maps to one plant.

3. **Each plant has four bays and 10 to 12 tools.** That is inside the demo bound of 3 to 5 bays and 8 to 16 tools. The family recipes and sensors stay in `data/fleet/mps-*.yaml`. `data/fleet/sites.yaml` places tools in bays and names the wafer-flow edges. Tools that are not in the original eight are clones of a template tool. The clone copies recipes and rewrites chamber and sensor ids. The fleet schema still checks the original eight. `sites.yaml` is not a family file, and the loader skips it.

4. **One seed, identical output.** Plant positions, telemetry samples, and throughput numbers are a function of `Mps:Demo:Seed`. The default is 4242, the same key lane R2 uses for reset. The same seed rebuilds the same coordinates, the same averages, and the same work-in-process counts. A different seed moves positions and the small random part of the throughput numbers. The 90-day enterprise history stays on its own seed, 20261014, so the existing fault evidence does not get new run ids. Every run, every manufacturing-system lot reference, and every material use in `enterprise.json` carries `siteId`. A material lot that is shared across plants does not get a single site. The use rows carry the sites.

5. **Ironwood etch runs hotter.** The etch family at Ironwood has a fixed offset on electrostatic chuck temperature and on chamber wall temperature, and a wider spread. The comparison uses Northwater as the reference plant. A plant is flagged only when its average sits more than two spreads away from Northwater, or when its spread is more than one and a half times Northwater's. The flag includes one plain sentence. Other parameters are not flagged.

6. **Scenario SC-5 is the cross-site supply case.** It is not S5. S5 remains the process-drift stretch case. SC-5 is precursor lot MX4419 from the fictional supplier Marrowgate Process Gases, used on deposition tool `D-03` at Northwater and `I-D-01` at Ironwood. Film thickness steps down on both. A chamber clean on `D-03` only is the confounder. The lot, the supplier notice, and the uses are in the enterprise seed. Ground truth stays in `data/groundtruth` and is not served to the agent. The manufacturing-system and material tools return the lot for either tool. Impact scope returns the material-lot scope, naming both plants, when the tool is one of those two. `D-01` is left alone so the contradictory-evidence case still reads as a document conflict.

7. **The console contract is additive.** `contracts/openapi.yaml` is version 1.2.0. New routes are `GET /api/sites`, `GET /api/sites/{siteId}`, and `GET /api/sites/compare?family=`. `Tool.siteId` is required. Older routes are unchanged. Examples live in `contracts/examples/sites/`.

## Consequences

- Lane S2 can draw Sites from the contract without waiting for this lane's API.
- Reset code that reads `Mps:Demo:Seed` changes the plant picture and does not rewrite the committed 90-day history.
- A new plant needs a prefix in `SiteIds`, a block in `sites.yaml`, and a note in this record.
- Agents can see which plant a run or a lot belongs to. They still cannot read ground truth.

## Confirmation

Tests check that seed 4242 rebuilds the same positions and throughput numbers, that a second seed differs, that Ironwood etch is flagged and Northwater etch is not, and that lot MX4419 is visible from both plants through the material tool and through impact scope.
