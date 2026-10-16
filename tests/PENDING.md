# Integration test catalog

`tests/Api.IntegrationTests` was written from the spec ahead of the lanes. As of 2026-10-16 every test runs in the default CI gate (`dotnet test MPS.sln`). There is no `PendingLane` trait and no project filter. `PendingLaneCatalogTests` fails if a trait, a filter, or a `Skip` comes back, or if a test is missing from this list.

Each host fixture gets its own SQLite folder (`MpsApiFactory`).

## Approval enforcement

| Test | Checks |
|---|---|
| `ApproveWithoutApproverIdDoesNotCreateACapa` | Missing approver id is 400 or 403 and stores no CAPA. |
| `ClientSuppliedSignatureIsRejected` | A client signature is ignored: approval uses a server-minted token and the client value never appears. |
| `RejectDoesNotCreateACapa` | A reject stores no CAPA and does not approve. |
| `RequestInfoDoesNotCreateACapa` | `requestInfo` returns the investigation and no CAPA. |
| `ApprovedRecommendationOpensOneCapaAndRejectsReuseAndSkips` | One CAPA; a repeat approve never opens a second; stage skip and backward move are conflicts. |

## Prompt injection

| Test | Checks |
|---|---|
| `OperatorInjectionTextIsStoredAndDoesNotOpenACapa` | Summary stored unchanged; starting an investigation opens no CAPA. |
| `OversizedOperatorTextIsRejected` | Summary length 4001 is 400. |
| `SummaryAtTheLengthLimitIsAccepted` | Summary length 4000 is accepted. |
| `DecisionCommentInjectionDoesNotApprove` | A reject comment with instructions does not approve or create a CAPA. |
| `RetrievedInjectionDocumentDoesNotAuthorizeAWrite` | The investigation is not approved and no CAPA is stored. |

## Service behavior

| Test | Checks |
|---|---|
| `FleetListsEveryConfiguredToolInFourFamiliesWithChambers` | 32 tools from `data/fleet/sites.yaml`, four families, at least two bays, every tool has a chamber. |
| `MetricsExposeModeAndSpend` | Mode is `replay` or `live`; spend present; cap positive. |
| `InjectedFaultIsAccepted` | `POST /api/faults` returns 202 with `faultId` and `correlationId`. |

## Tool failures (mock enterprise host)

| Test | Checks |
|---|---|
| `CreateCapaWithInjectedServerErrorReturns500` | `X-Mock-Fault: 500` returns 500. |
| `CreateCapaWithInjectedConflictReturns409` | `X-Mock-Fault: 409` returns 409. |
| `RepeatingACapaCreateIsAnIdempotentConflict` | A repeat create is 409. |
| `CreateCapaTimeoutDoesNotSucceed` | A timeout never succeeds. |
| `CreateCapaWithoutApprovalTokenIdIsRejected` | No approval token is 400. |
| `OpenTicketWithInjectedServerErrorReturns500` | Ticket create with a 500 fault returns 500. |
| `GroundTruthIsNotOnTheMockEnterpriseApi` | Ground-truth paths are 404. |

## Moved out of this project

The two evidence-escalation tests (`MissingEvidenceEscalates…`, `ContradictoryEvidenceNamesTheConflict…`) were removed. The integration host uses the fake workflow, which cannot escalate. The same behavior is covered by:

- `InvestigationWorkflowTests.S2ReplayEscalatesOnTheRevisionConflictAndDoesNotRankACause` (Orchestration.Tests);
- eval cases MIS-01..03 and CON-01..02 (`eval/cases`, CI step "Eval (replay mode, gates)").

Category checks moved out of `ApprovedRecommendation…` into eval cases ECO-01..04. Ranker-level injection checks moved into eval cases INJ-01..02.
