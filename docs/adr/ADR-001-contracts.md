# ADR-001: Shared contracts

- Status: Proposed
- Date: 2026-10-14
- Deciders: GB-F drafted this. Bob reviews it and tags `contracts-v1`. This change set does not create that tag.

## Context

Parallel lanes need one shape for incidents, evidence, approvals, and the console API before they write code. The domain rules that must stay repeatable live in `Mps.Domain`. Wire contracts live beside the code so a UI mock and a later API can follow the same document.

## Decision

1. Domain types in `Mps.Domain` are the in-process contract. Identifiers are dedicated types. A CAPA can be opened only with an `ApprovalToken`, and rehydration still requires the token id. The token is HMAC-SHA256, single-use, expiring, and bound to the SHA-256 of the incident id and the CAPA summary. The signing key is an argument. It is not stored on the token and is not an agent input.
2. Read-only agent tools are `Mps.Application.Tools`. Write ports are `Mps.Application.Writes`. The NetArchTest suite rejects an agent dependency on `Mps.Application.Writes`. `IInvestigationWorkflow` (`StartAsync`, `ResumeWithHumanInputAsync`, `GetStateAsync`) is the orchestration boundary. `LangGraphWorkflowAdapter` is a stub and throws. It is not a second runtime.
3. Incident stages, from `TicketOpened`: Investigating, then RecommendationReady or Escalated. RecommendationReady goes to AwaitingApproval or Escalated. AwaitingApproval goes to Approved, Rejected, or Escalated. Approved and Rejected can close. Rejected and Escalated can return to Investigating. Skipping to Approved is rejected.
4. CAPA stages move one step at a time: Open, Containment, RootCauseConfirmed, CorrectiveAction, Verification, Closed. A failed verification does not move backward in this contract.
5. The console HTTP contract is `contracts/openapi.yaml` (OpenAPI 3.0.3). JSON enums are camelCase (`engineeringChange`, `ticketOpened`). The server issues approval tokens. Clients send `approverId` on approve and never send a signature. Mock enterprise HTTP, including `X-Mock-Fault` of `500`, `timeout`, or `409`, is `contracts/mock-enterprise.openapi.yaml`.
6. Lifecycle events are JSON Schema draft 2020-12 documents in `contracts/events/`. Each event has `eventId`, `eventType`, `occurredAt`, `correlationId`, and `payload`. `eventType` is camelCase. The catalog is `alertRaised`, `ticketOpened`, `incidentOpened`, `investigationStarted`, `evidenceGathered`, `hypothesisProposed`, `investigationEscalated`, `approvalGranted`, `approvalRejected`, `capaCreated`, `capaAdvanced`, and `faultInjected`. `contracts/events/event.schema.json` is the `oneOf` of those documents. Approval events do not carry a token signature.
7. `contracts/msw/handlers.ts` is a hand-written MSW 2 handler set that follows `contracts/openapi.yaml`. The web app is not on this branch, so the handlers are not executed here.

## Consequences

- Lanes code against these types and files. After Bob tags `contracts-v1`, a change needs a design-question issue, a new ADR, and Bob's approval.
- The API project does not implement these routes yet. The document is the contract. The host exposes `GET /health` only.
- Ticket numbers start with `TKT-`.
- Hypothesis confidence is from 0 to 1, and a hypothesis cites at least one evidence id. An escalation names the gap or the conflict.

## Confirmation

`dotnet test` covers the CAPA token rules, hypothesis citation, and the illegal incident and CAPA transitions. The layering suite covers the write-port boundary.

## Addendum: contracts-v1.1 (2026-10-14, Bob)
- `DecisionResult.capa` nullable via `allOf` wrapper (OpenAPI 3.0 keeps `nullable` beside `allOf`; GB-5 DQ1).
- Approve endpoint wording: client signature is rejected (400), not ignored (GB-5 DQ2).
- `EvidenceItem.whyItMatters` (nullable, <=240 chars) and `EvidenceItem.signalId` (fleet sensor id) per ADR-003 / UX3.
- `EvidenceStance`, `EvidenceMatrixEntry`, optional `Investigation.evidenceMatrix` (UX3).
- All additions are optional, so v1 clients remain compatible. Spec version 0.2.0.

## Addendum: contracts-v1.2 (2026-10-14, GB-3)

Additive console paths. Existing operations are unchanged. OpenAPI `info.version` is 0.3.0. Bob tags `contracts-v1.2`.

- `GET /api/admin/session` returns `mode` (`local` or `azure`), `isAdmin`, and `displayName`. It is not admin-gated.
- `POST /api/admin/reset` is admin-only. Local mode with no Easy Auth principal header is the local dev user and is an admin. A present header requires a role claim of `admin`, including in local mode, so a non-admin can be rejected without switching mode. Azure mode without a valid admin principal is 403. The body restores fleet state, tickets, investigations, approvals, CAPAs, and the simulated clock from `Mps:Demo:Seed` (default 4242). The same seed is used locally and in Azure. A second call returns that same baseline.
- `GET /api/injector/scenarios` lists the named scenarios. `POST /api/injector/scenarios/{scenarioId}` runs one and returns the seeded telemetry. One scenario covers each root-cause class: engineering change, equipment calibration, supply-chain material, and process drift. `POST /api/faults` remains the free-form injector.

```
header: X-MS-CLIENT-PRINCIPAL
scenarios: SC-1 SC-2 SC-3 SC-4
```
- `contracts/msw/handlers.ts` covers the new operations. No request or response field on an existing operation changed.
- (lane T1) `GET /api/incidents/{incidentId}/investigation` returns the latest investigation for that incident. 404 when the incident does not exist or no investigation was started. A ticket opens by its incident id; a ticket id or number is not an investigation id.
- `HumanDecisionKind` gains `override` (ADR-005).

## Addendum: contracts-v1.3 (2026-10-15, GB-HIST)

Additive console paths and query parameters. Existing operations are unchanged. OpenAPI `info.version` is 1.3.0.

- `GET /api/investigations` returns every investigation, newest first. A later start on the same incident sorts before the earlier start. A newer incident sorts before an older one. The item shape is the existing `Investigation` schema.
- `GET /api/alerts`, `GET /api/tickets`, `GET /api/investigations`, and `GET /api/capas` accept an optional `siteId` query. When it is set, the list keeps records for tools at that plant. An unknown plant returns an empty list, not a missing-record error. Omit it and the list is unchanged. Alert lists still accept `toolId`. Both filters apply together.
