# Design questions: GB-3 round 2 (P4b)

Lane `lane/3b-p4b`. Issue #65. Contracts and Domain were not changed. Each item has context, options, a recommendation, and whether it blocked the lane.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

Round 1 questions stay in `docs/design-questions/GB-3.md`. Two of them are answered here and left in that file as history.

## Closed from round 1

### 4. A later approve after a stored QMS failure

Answered in `82ef0b2`. A later explicit approve issues a new single-use token and retries the CAPA write. The old token stays consumed. Both attempts are audited (`Sequence` orders them). Success and an in-flight `writing` row still resume the same token. A double-click in one process is idempotent. Blocking: no.

### 5. Fault injector side effect

Answered in `bdb4eac`. `Mps:Injector:DirectTicket` defaults to false in `appsettings.json` and when the key is missing. The fault is still stored and notified. Set the flag to true for a demo that has no simulator. Blocking: no.

## 1. Domain has no place for the v1.1 evidence narrative

Context: contracts v1.1 added optional `EvidenceItem.whyItMatters` (max 240), `EvidenceItem.signalId`, `EvidenceStance`, `EvidenceMatrixEntry`, and optional `Investigation.evidenceMatrix` (ADR-001 addendum, ADR-003). `Mps.Domain.Evidence.EvidenceItem` still constructs from id, source, title, excerpt, observedAt, toolId, and externalRef. `InvestigationSnapshot` has no matrix. Domain is the in-process contract, and this lane does not edit it.

The API stores the narrative beside the snapshot. `FakeInvestigationWorkflow` calls `IInvestigationNarrativeLog.Stage` before the row is inserted. `EfQualityStore` writes the notes into `EvidenceJson` and the matrix into `EvidenceMatrixJson`, and it keeps them across approve and reject copies. HTTP and SignalR `investigationChanged` return the fields. The agent workflow in the other lane does not call `Stage`. When that workflow replaces the fake, the fields stay empty unless something else stages a narrative.

Options:

- Add optional `whyItMatters` and `signalId` on `EvidenceItem`, and an optional evidence matrix on `InvestigationSnapshot`, so the agent lane sets them on the domain object and the API maps them. The sidecar can then be retired.
- Keep the sidecar. Document that any workflow implementation calls `IInvestigationNarrativeLog.Stage` with the same investigation id before `IInvestigationLog.AddAsync`.

Recommendation: add the optional Domain properties. They match OpenAPI, they give GB-2 a place to set the values, and they remove a side channel that only the fake knows about. The sidecar is enough for this demo and does not block the pitch. Blocking: no.

## 2. `signalId` length cap is only in the application

Context: OpenAPI limits `whyItMatters` to 240 characters and does not limit `signalId`. `InvestigationNarrative` rejects a why longer than 240, which matches the contract, and rejects a `signalId` longer than 200. A control character is rejected on both. `data/fleet` has tool, chamber, and recipe ids and no sensor catalog, so the stand-in id is `spc.stand-in`.

Options:

- Add `maxLength: 200` (or another bound) to `signalId` in OpenAPI, and point `signalId` at a real fleet sensor id once that catalog exists.
- Drop the application cap and accept any string the contract allows.

Recommendation: keep a finite application cap so a huge id cannot land in SQLite, and add the same max to OpenAPI when Bob next opens the contract. `spc.stand-in` should be replaced when the fleet files name a sensor. Blocking: no.

## 3. Approval retry gate is per process

Context: two approve clicks in one API process share a static `SemaphoreSlim` keyed by investigation id. The second click sees `succeeded` and does not write again. Two processes do not share that gate. SQLite still has one row per token, and a resumed `writing` row reuses its token, so a crash between the insert and the QMS response is safe. Two processes that both observe `failed` or no row can mint two tokens.

Options:

- Leave the gate in process. The demo runs one API.
- Take a SQLite transaction or a unique partial index so only one `writing` row can exist per investigation.

Recommendation: leave the in-process gate for the pitch. A database constraint is the durable fix if the API is ever run as more than one process. Blocking: no.
