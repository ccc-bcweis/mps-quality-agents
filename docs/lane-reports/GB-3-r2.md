# Lane report: GB-3 round 2 (P4b)

Branch `lane/3b-p4b`. Issue #65. Local commits only. Nothing was pushed. Contracts, Domain, and the other lanes' projects were not changed.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company.

| Work | Commit | What landed |
|---|---|---|
| Stored QMS failure, then a later approve | `82ef0b2` | A later explicit approve mints a new single-use `ApprovalToken` and retries the CAPA write. The old token stays consumed. Both attempts are audited. |
| Fault injector side effect | `bdb4eac` | Direct ticket creation defaults off (`Mps:Injector:DirectTicket` is false). The fault is still recorded and `faultInjected` is still notified. The flag stays available for a self-contained demo. |
| Evidence narrative | `c9352d5` | `whyItMatters`, `signalId`, and `evidenceMatrix` travel through the HTTP API, SignalR `investigationChanged`, and SQLite. Domain types still omit these fields, so they live in an application sidecar. |

Base is `d5dd2be` (contracts v1.1, tag `contracts-v1.1`). Round 1 stays in `docs/lane-reports/GB-3.md`.

## Stored failure retry

`HumanDecisionService.ApproveAsync` takes a per-investigation gate, reloads the investigation and the incident inside that gate, and then:

- No approval row, or the latest row is `failed`: mint a new token, persist a new approval row at `writing`, and call the QMS writer. The previous row is left as consumed and failed.
- Latest row is `succeeded`: return that CAPA. A second click does not write again.
- Latest row is still `writing`: resume that same token. The in-request retry (three attempts on 500 and 408, 409 with an existing id counted as success) is unchanged.

`ApprovalRow.TokenId` remains the primary key, so one investigation can have several rows. `Sequence` is assigned in `EfQualityStore.AddAsync` as one more than the current max for that investigation, so a retry sorts correctly when the clock does not move. `FindByInvestigation` returns the newest row. `ListByInvestigationAsync` returns oldest first.

The gate is an in-process `SemaphoreSlim`. Two clicks in one API process share it. Two processes do not.

## Injector flag

`FaultInjectionService` always validates the tool, stores the `FaultRecord`, and notifies `FaultInjected`. When `InjectorPolicy.DirectTicket` is false it returns `FaultAccepted` with an empty incident id and does not call intake or SPC. When the flag is true it still opens a `faultInjector` incident and, when the absolute magnitude is greater than 3, raises an SPC alert.

`QualityHost` reads `Mps:Injector:DirectTicket` and defaults the missing key to false. `src/Mps.Api/appsettings.json` sets it to false. `POST /api/faults` still responds 202 with `faultId` and `correlationId` only.

## Evidence narrative

`EvidenceItem` and `InvestigationSnapshot` in Domain do not carry `whyItMatters`, `signalId`, or `evidenceMatrix`. OpenAPI v1.1 does. This lane did not edit Domain or `contracts/openapi.yaml`.

`InvestigationNarrative` in `Mps.Application.Evidence` holds the notes and the matrix. `IInvestigationNarrativeLog.Stage` records a narrative for an investigation id. `EfQualityStore` is both `IInvestigationLog` and `IInvestigationNarrativeLog`. On insert it applies a staged narrative onto `EvidenceJson` (`WhyItMatters`, `SignalId`) and `InvestigationRow.EvidenceMatrixJson`. On update it keeps prior notes by evidence id and drops matrix rows whose evidence id is gone, so approve and reject copies do not wipe the sidecar.

`whyItMatters` is trimmed, stored as null when blank, limited to 240 characters (the OpenAPI max), and rejected when it contains a control character. `signalId` is trimmed, stored as null when blank, and limited to 200 characters in this application. That 200 is not in OpenAPI. Duplicate notes for one evidence id, a repeated category on the same evidence id, an unknown evidence id, and `Guid.Empty` are rejected with `IntakeValidationException` (HTTP 400).

`FakeInvestigationWorkflow` stages a plain-language stand-in before it returns: signal id `spc.stand-in`, and a four-row matrix (engineering change rules out, calibration neutral, material rules out, process drift supports). `data/fleet` has no sensor catalog, so the id is a stand-in label. The real agent workflow does not call `Stage`. Until it does, or until Domain grows the properties, replacing the fake leaves the fields empty.

`IQualityNotifier` is scoped so `SignalRQualityNotifier` can read the scoped store. JSON enums stay camelCase (`supports`, `rulesOut`, `neutral`, `processDrift`).

## How it was verified

Before each code commit, and again for this report: `dotnet test MPS.sln --configuration Release` (warnings are errors) and `dotnet format MPS.sln --verify-no-changes --severity warn`.

This closeout run passed 380 tests: Application 167, API 13, Domain 19, ModelGateway 13, Agents 58, Simulator 36, Orchestration 14, MockEnterprise 4, Connectors 7, Architecture 10, Contract 39. Failed 0.

Application tests named for this round:

- `AStoredFailureThenALaterApproveIssuesANewTokenAndSucceeds` audits both tokens and shows the first token still consumed.
- `ADoubleClickApproveIsIdempotentForTheSameToken` records one writer call and one succeeded row.
- `DirectTicketDefaultsOffAndStillRecordsTheFault` and `DirectTicketOpensAnIncidentAndALargeMagnitudeRaisesAnAlert`.
- Narrative create tests cover the 240-character why, a blank why stored as null, a 241-character rejection, a control character, an unknown evidence id, a duplicate note, and a repeated matrix category.

API tests:

- `FaultInjectionAcceptsTheFaultWithoutOpeningATicketByDefault`.
- `FaultInjectionOpensTheInjectorChannelWhenDirectTicketIsEnabled` sets `Mps:Injector:DirectTicket` to true on the host.
- `InvestigationKeepsWhyItMattersSignalIdAndTheEvidenceMatrix` starts an investigation, rejects it, and reads the narrative back, including camelCase `processDrift` / `supports` and `engineeringChange` / `rulesOut`.

`Api.IntegrationTests` still reports that the filter `Category!=PendingLane` matches nothing and exits 0. That filter was already empty before this round.

Docker Compose was not started. Azure CLI was not used. No git network command was run.

## Behavior Bob should expect

- `EnsureCreated` builds a new SQLite file. It does not add columns to an existing `mps.db`. Delete that file so `Approvals.Sequence` and `Investigations.EvidenceMatrixJson` exist. Old evidence JSON still deserializes: the new document fields default to null.
- Round 1 design question 4 is closed: a later approve after a stored failure issues a new token and retries the write.
- Round 1 design question 5 is closed: the injector side effect defaults off. Set `Mps:Injector:DirectTicket` to true when the pitch needs the API to open the ticket by itself.
- The demo approval key is still the sequential bytes 1 through 16. `APPROVAL_SIGNING_KEY` (hex) overrides it. Token lifetime still defaults to 15 minutes.

## Open questions for Bob

Written up in `docs/design-questions/GB-3-r2.md`. None of them blocked this lane. The one that matters when the agent workflow replaces the fake: Domain still has nowhere to put `whyItMatters`, `signalId`, or `evidenceMatrix`, so the real workflow has to call `IInvestigationNarrativeLog.Stage` or those fields stay empty.
