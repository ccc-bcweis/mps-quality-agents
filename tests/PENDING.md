# Pending integration tests

`tests/Api.IntegrationTests` is written from the spec and the frozen contracts, ahead of the lanes that implement them. Every fact in that project carries the xUnit trait `Category=PendingLane`. The project sets `VSTestTestCaseFilter` to `Category!=PendingLane`. CI runs `dotnet test` with no filter of its own, so these tests stay out of the merge gate and main stays green.

Run the held tests on purpose:

```powershell
dotnet test tests/Api.IntegrationTests --filter Category=PendingLane
```

They fail until the owning lane hosts the behavior. A test leaves this list when its trait is removed. `PendingLaneCatalogTests` requires every fact in the assembly to stay pending, to have no `Skip`, and to appear below. That stops a test from vanishing under the not-equal filter.

## Approval enforcement

Spec sections 2.5, 5, and 7.4. Unblocked by GB-3 (P3 lifecycle, P4 approval and CAPA) and, for the S1 recommendation, GB-2 (A4 workflow).

| Test | Waiting on |
|---|---|
| `ApproveWithoutApproverIdDoesNotCreateACapa` | GB-3 P4. Missing approver id is 400 or 403 and stores no CAPA. |
| `ClientSuppliedSignatureIsRejected` | GB-3 P4. `HumanDecisionRequest` rejects unknown properties, including a client signature. |
| `RejectOnAnEscalatedInvestigationDoesNotCreateACapa` | GB-2 A4 and GB-3 P4. A reject on an escalated investigation stores no CAPA. |
| `RequestInfoDoesNotCreateACapa` | GB-3 P4. `requestInfo` returns the investigation and no CAPA. |
| `ApprovedRecommendationOpensOneCapaAndRejectsReuseAndSkips` | GB-2 A4 and GB-3 P3/P4. S1 on E-02 `escTemperature`: one CAPA, reuse rejected, stage skip and backward move are conflicts. |

## Evidence escalation

Spec sections 2.4, 2.5, and 2.7 (S2). Unblocked by GB-2 (A2 evidence gate, A4 workflow) through the GB-3 investigation API.

| Test | Waiting on |
|---|---|
| `MissingEvidenceEscalatesWithoutAssertingARootCause` | GB-2 A2/A4. Stage `escalated`, reason present, hypotheses empty, no CAPA. |
| `ContradictoryEvidenceNamesTheConflictAndDoesNotAssertARootCause` | GB-2 A2/A4. S2 on D-01 `chamberPressure`: reason names the rev C / rev B conflict and asserts no root cause. |

## Prompt injection

Spec sections 4.2, 5, and 7.4. Operator text is stored as data. A retrieved injection document does not authorize a write.

| Test | Waiting on |
|---|---|
| `OperatorInjectionTextIsStoredAndDoesNotOpenACapa` | GB-3 P2. The summary is stored unchanged and starting an investigation does not open a CAPA. |
| `OversizedOperatorTextIsRejected` | GB-3 P2. Summary length 4001 is 400. |
| `SummaryAtTheLengthLimitIsAccepted` | GB-3 P2. Summary length 4000 is accepted. |
| `DecisionCommentInjectionDoesNotApprove` | GB-3 P4. A reject comment that contains instructions does not approve or create a CAPA. |
| `RetrievedInjectionDocumentDoesNotAuthorizeAWrite` | GB-1 D5 and GB-2 A3. Evidence includes a document, the stage is not `approved`, and no CAPA is stored. |

## Service behavior

Spec sections 2.1, 2.7, and 2.8.

| Test | Waiting on |
|---|---|
| `FleetListsEightToolsAcrossTwoBaysAndFourFamilies` | GB-1 D1 and GB-3. Eight tools, two bays, each family twice, every tool has a chamber. |
| `MetricsExposeModeAndSpend` | GB-2 A1 and GB-3 P5. Mode is `replay` or `live`. Spend is present and the cap is positive. |
| `InjectedFaultIsAccepted` | GB-1 D2/D3 and GB-3. `POST /api/faults` returns 202 with `faultId` and `correlationId`. |

## Tool failures

Spec sections 2.5, 2.6, and 7.4. These call the mock enterprise host directly. Unblocked by GB-1 D4, which must give `Mps.MockEnterprise` a web entry point. Until then the fixture fails fast because the assembly has no entry point.

| Test | Waiting on |
|---|---|
| `CreateCapaWithInjectedServerErrorReturns500` | GB-1 D4. `X-Mock-Fault: 500` on `POST /qms/capas` returns 500. |
| `CreateCapaWithInjectedConflictReturns409` | GB-1 D4. `X-Mock-Fault: 409` returns 409. |
| `RepeatingACapaCreateIsAnIdempotentConflict` | GB-1 D4. The same create body is 201, then 409. |
| `CreateCapaTimeoutDoesNotSucceed` | GB-1 D4. `X-Mock-Fault: timeout` cancels or returns a non-success status. |
| `CreateCapaWithoutApprovalTokenIdIsRejected` | GB-1 D4. A create without `approvalTokenId` is 400. |
| `OpenTicketWithInjectedServerErrorReturns500` | GB-1 D4. `X-Mock-Fault: 500` on `POST /ticketing/tickets` returns 500. |
| `GroundTruthIsNotOnTheMockEnterpriseApi` | GB-1 D3/D4. Ground-truth paths on the mock host are 404. |
