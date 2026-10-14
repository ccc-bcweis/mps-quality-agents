# Design questions: GB-3 API

Lane `lane/3-api`. Contracts were not changed. Each item has context, options, a recommendation, and whether it blocked the lane.

## 1. SPC and lifecycle rules live in Application

Context: SPEC and PLAN place the Western Electric evaluator and the lifecycle rules in `Mps.Domain`. Domain is frozen for this lane. It already has `IncidentLifecycle`, `CapaLifecycle`, and `SpcViolation`. It has no evaluator and no `Incident.Rehydrate`.

Options:

- Add the evaluator and a rehydrate method to Domain in a foundation change.
- Keep the frozen lifecycle tables in Domain and put the evaluator, debounce, intake, and stage walking in `Mps.Application`.

Recommendation: keep the rules in Application. The domain tables still reject illegal transitions. Blocking: no.

## 2. Restoring an incident stage

Context: `Incident.Open` always starts at `TicketOpened`. Persistence has to load a later stage.

Options:

- Add `Incident.Rehydrate` to Domain.
- Replay the legal path from `TicketOpened`, and store `HasEscalated` in its own column so a critical incident does not lose the escalation flag.

Recommendation: replay plus the column. `EnsureCreated` is the local schema. There are no migrations. Delete `mps.db` if an older file is already present. Blocking: no.

## 3. Tickets and the approval token

Context: CAPA creation consumes an `ApprovalToken`. `ITicketingPort.OpenAsync` has no token parameter. P1 auto-opens a `TKT` record from a high or critical SPC alert before any human decision.

Options:

- Add a token parameter to the ticketing port. That is a contract change.
- Treat intake tickets as pre-approval `TKT` records. Require a valid explicit token only for the CAPA write.

Recommendation: token on the CAPA write only. A client never sends a signature. The server issues the token when `approverId` is present. Blocking: no.

## 4. QMS write failure and the decision response

Context: SPEC says a QMS 500 is retried and then the investigation fails, and a 409 is idempotent. `contracts/openapi.yaml` lists decision responses 200, 400, 403, and 404.

Options:

- Add a 502 or 503 to the contract.
- After three immediate attempts, return 200 with `investigation.stage` of `failed` and `capa` null. Persist the approval, the attempt count, and the failure detail. Status 409 with an existing external id is success and stores that id.

Recommendation: the 200 plus `failed` result, so the response still matches `DecisionResult`. A second approve of a stored failure returns that result and does not call the writer again. The in-request retry is what turns one 500 into success. Blocking: no. Bob: should a later approve be allowed to issue a new token after a stored failure?

## 5. Fault injection side effect

Context: SPEC says the alert follows from the simulator. `POST /api/faults` still returns 202 with `faultId` and `correlationId`. It also opens a `faultInjector` incident, and when the absolute magnitude is greater than 3 it evaluates a one-point SPC series so this lane's demo raises an alert without the simulator. Dedupe collapses a second report for the same tool and parameter.

Options:

- Keep the side effect so the API demo is self-contained.
- Add a switch that defaults off and let the simulator own the incident and the alert.

Recommendation: keep the side effect for the pitch. Bob: say if it should default off once the simulator lane is merged. Blocking: no.

## 6. Debounce key includes the chamber

Context: SPEC describes debounce per tool and parameter. The implementation key is tool, chamber, and parameter. Parameter and chamber compare case-insensitively. An age equal to the window does not suppress.

Options:

- Debounce on tool and parameter only.
- Keep the chamber in the key so two chambers are separate signals.

Recommendation: keep the chamber in the key. Blocking: no.

## 7. External CAPA id is stored and not returned

Context: `IQmsCapaWriter.CreateAsync` returns the QMS id. The row stores it. The OpenAPI `Capa` schema has no `externalId`.

Options:

- Add `externalId` to the contract.
- Keep the id in SQLite until the console needs it.

Recommendation: keep it internal. Blocking: no.

## 8. ADR-002 is not in the repo

Context: the lane brief names ADR-002. The repository has `docs/adr/ADR-001-contracts.md` only.

Options:

- This lane writes an ADR-002.
- Foundation or Bob adds it if the number referred to a decision that was never filed.

Recommendation: Bob confirms the missing ADR. This lane did not invent one. Blocking: no.
