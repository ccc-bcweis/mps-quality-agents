# Lane report: GB-5 QA, eval, and security

Branch `lane/5-qa`. Commits are local. Nothing was pushed.

| Issue | Commit | What landed |
|---|---|---|
| T1 (#29) | `b6ea086` | `tests/Contract.Tests` written from `contracts/openapi.yaml`, `contracts/mock-enterprise.openapi.yaml`, `contracts/events/`, `contracts/msw/handlers.ts`, and the spec's enum and tool names. `src/Mps.Api/ApiHostMarker.cs` lets `WebApplicationFactory` host the console. |
| T2 (#30) | `a44ec98` | `tests/Api.IntegrationTests` for approval, bad and contradictory evidence, prompt injection, fleet, metrics, fault injection, and mock tool failures (500, timeout, 409). Every fact is `Category=PendingLane` and listed in `tests/PENDING.md`. `PendingLaneCatalogTests` guards the list and the filter. `src/Mps.MockEnterprise/MockHostMarker.cs` is the factory marker for when GB-1 D4 adds a web entry point. |
| T3 (#31) | `706f297` | `stryker-config.json` (Domain, break 60), `stryker-application.json` (Application, break 0), `.config/dotnet-tools.json` pins `dotnet-stryker` 4.16.0, and `.github/workflows/quality.yml` (nightly 06:00 UTC and `workflow_dispatch`). `eval/README.md` is the E1 plan for promptfoo and Microsoft.Extensions.AI.Evaluation. `ci.yml` was not edited. |

The GB-F foundation report that used to sit in this file is still in commit `765f364`.

## How it was verified

- Before the T2 and T3 commits, `dotnet format MPS.sln --verify-no-changes` exited 0, and `dotnet build` was warnings-as-errors clean.
- `dotnet test MPS.sln --configuration Release` passed Domain.Tests 19, Architecture.Tests 10, and Contract.Tests 39. Api.IntegrationTests reported no match for `Category!=PendingLane`, and the solution run stayed green.
- `dotnet test tests/Api.IntegrationTests --configuration Release --filter Category=PendingLane` executed 25 cases and failed them for the current tree. The mock-enterprise cases throw because `Mps.MockEnterprise` has no entry point. The console cases receive HTTP 404 because those routes are not hosted yet.
- Stryker 4.16.0 against Domain (`FullyQualifiedName~Mps.Domain.Tests`, 19 tests) created 455 mutants and tested 146. The score was **27.68%**: 98 killed, 48 survived, 208 with no coverage. The process exited 1 because the break is 60. The HTML report is under `StrykerOutput/domain/reports/` and is gitignored.
- Stryker against Application (`FullyQualifiedName~Mps.Architecture.Tests`, 10 tests) tested 0 mutants. The only mutant was `LayerAnchor.cs`, which the config excludes. The score was N/A and the process exited 0.
- Docker, the Azure CLI, and GitHub were not used. `quality.yml` has not been run.

## What Bob should expect from CI

`ci.yml` is unchanged. Pending integration tests stay out of that run because `tests/Api.IntegrationTests/Api.IntegrationTests.csproj` sets `VSTestTestCaseFilter` to `Category!=PendingLane`. A command-line `--filter` overrides the property, which is how the held tests are run on purpose. `PendingLaneCatalogTests` fails if a new integration fact lacks the trait, has a `Skip`, or is missing from `tests/PENDING.md`.

Running Stryker from the repo root uses solution mode, so the configs set `test-case-filter` to the domain or architecture test namespace. That keeps the pending suite out of the mutation run. A `Category!=PendingLane` filter was not used there, because a not-equal category filter can drop tests that do not carry the trait.

`quality.yml` publishes `quality-coverage-report` and `mutation-reports`. The mutation HTML is uploaded even when the score step fails. The workflow does not run on pull requests. Leave it off the required checks until the Domain score is at least 60%. The score is 27.68% today, mostly from types the domain tests do not cover (`EnterpriseRecords`, `EvidenceItem`, `TelemetryWindow`, `ImpactScope`).

## Open questions

1. An approve with no `approverId` accepts 400 or 403 in the integration tests, and must store no CAPA. SPEC 7.4 says a missing token is 403. Say if you want the test to require 403 only.
2. Two contract wordings are in `DESIGN_QUESTIONS.md`. Both are non-blocking, and the contract files were left as tagged. `DecisionResult.capa` should wrap its `$ref` in `allOf` if generators need to see null. The decision description should say a client signature is rejected, matching `additionalProperties: false`.
3. `E-02`, `D-01`, `S-01`, and `C-01` are scenario tool ids from the spec. The console schema types `toolId` as a string, so they need no contract change.
4. The console OpenAPI has no operation that arms `X-Mock-Fault` on a connector call. The mock-host tests cover 500, timeout, and 409 directly. The spec path "QMS 500, then retry, then Failed" starts working when the investigation calls that host. The lane that arms the fault for the live demo is yours to assign.
5. Expired, reused, tampered, and wrong-action tokens are already covered in `tests/Domain.Tests/InvariantTests.cs` (`AnExpiredTokenCannotOpenACapa`, `ATokenCannotOpenTwoCapas`, `ATamperedSignatureCannotOpenACapa`, `ATokenBoundToAnotherActionCannotOpenACapa`). The HTTP tests add a second approve and an illegal CAPA stage move once P4 exists.
6. `ApiHostMarker` and `MockHostMarker` are the only product files this lane added. `WebApplicationFactory` cannot take the static `LayerAnchor` type.
7. Application mutation stays at break 0 until that project has behavior beyond interfaces. A zero-mutant run exits 0 with Stryker 4.16.0.
