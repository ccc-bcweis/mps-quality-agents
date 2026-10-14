# Lane report: GB-3 API

Branch `lane/3-api`. Local commits only. Nothing was pushed. Contracts, Domain, and the other lanes' projects were not changed.

| Issue | Commit | What landed |
|---|---|---|
| P1 (#20) | `5441343` | Western Electric rules 1-4, the specification window, and the six-point rising trend. Debounce. High and critical alerts open one intake ticket. |
| P2 (#21) | `d0bf97d` | IoT, operator, NCR, supplier notice, and fault-injector intake. Dedupe on an open tool and parameter. Severity floors and escalation. |
| P3 (#22) | `9e806a6` | Lifecycle walking, SQLite store, REST routes from `contracts/openapi.yaml`, SignalR hub `/hubs/fleet`. Illegal transitions return 409. |
| P4 (#23) | `7d3a7d7` | Server-issued single-use approval token. CAPA write only after that token is consumed. QMS retries and 409 idempotency. Approval and result persisted. |
| P5 (#24) | `fc7b888` | `GET /api/metrics` rollup and OpenTelemetry traces and meters. OTLP export only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. |

## How it was verified

Before each commit: `dotnet test MPS.sln --configuration Release` (warnings are errors, so the build is part of the test) and `dotnet format MPS.sln --verify-no-changes --severity warn`.

Latest full run, after P5: 158 application tests, 11 API tests, 19 domain tests, 10 architecture tests. All passed. Format verify exited 0.

API tests use `WebApplicationFactory` against a temporary SQLite file. They cover health, the eight fictional tools, operator dedupe, validation, investigation start, reject, approve with a token, a repeated approve, illegal CAPA advance, fault injection, SignalR `incidentChanged`, and the metrics rollup.

Application tests cover the SPC series (including points that must not fire), debounce, one test per intake channel, every illegal incident and CAPA move, and approval enforcement: missing approver, reject does not write, expired token, tampered signature, hash mismatch, reused token, 500 then success, 408 then success, three 500s then `failed` with no CAPA, 409 with an existing id, and a client error that is not retried.

Docker Compose was not started. Azure CLI was not used. The Aspire dashboard was not running, so OTLP export was not observed live. The SDK is registered, and the exporter is added only when the endpoint variable is set. Tests set `Otel:Enabled` to false.

## Behavior Bob should expect

- JSON enums on HTTP and SignalR are camelCase (`operatorReport`, `ticketOpened`, `recommendationReady`).
- SQLite timestamps are UTC `DateTime` values. `DateTimeOffset` cannot be ordered by the SQLite provider. `EnsureCreated` builds the schema. There are no migrations. Delete `mps.db` if a file from an earlier run is still there.
- The demo approval key is the sequential bytes 1 through 16. It is not a production secret. `APPROVAL_SIGNING_KEY` (hex) overrides it. The token lifetime defaults to 15 minutes.
- Intake `TKT` records do not take an approval token. A CAPA does. Details are in `DESIGN_QUESTIONS.md`.
- `GET /api/metrics` leaves `categoryAccuracy` null until the eval lane has a score. p95 uses nearest rank: index `ceil(0.95 * n) - 1`. Replay cost defaults to 0, which is a real number once an investigation succeeds. Spend today sums cost recorded on the current UTC date. The cap defaults to $2.
- The activity source and the meter are both named `Mps.Quality`. The string `Mps.Api` makes NetArchTest report an Application dependency on the API project.
- `POST /api/faults` also opens a fault-injector incident and, above 3 sigma, an SPC alert. The response body is still `faultId` and `correlationId`.

## Open questions for Bob

The write-ups, options, and recommendations are in `DESIGN_QUESTIONS.md`. None of them blocked the lane. The one that changes a later demo click: after a stored QMS failure, a second approve currently returns the failed investigation and does not issue a new token.
