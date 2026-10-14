# Lane report: GB-2 agents

Branch `lane/2-agents`. Local commits only. Nothing was pushed. Contract files were not changed. No `DESIGN_QUESTIONS.md`: nothing here blocks on a contract edit.

| Issue | Commit | What landed |
|---|---|---|
| A1 (#16) | `c7565d6` | Model gateway with Azure OpenAI, OpenAI, replay, and mock-reasoner adapters, an in-memory cost ledger, and a daily spend cap |
| A2 (#17) | `5fe9799` | Impact features, evidence gate, and grounding validator in `Mps.Agents` |
| A3 (#18) | `b503d05` | Investigation agent (read-only tools), resolution agent (cited JSON only), versioned prompts in `prompts/` |
| A4 (#19) | `449e4ad` | `MafInvestigationWorkflow` (`IInvestigationWorkflow`), checkpoint, human-in-the-loop resume, `docs/langgraph-compat.md` |

## How it was verified

`dotnet format MPS.sln --verify-no-changes` exited 0 before the A4 commit. `dotnet test MPS.sln --configuration Release` then passed with warnings as errors:

| Project | Passed |
|---|---|
| Domain.Tests | 19 |
| ModelGateway.Tests | 13 |
| Agents.Tests | 58 |
| Orchestration.Tests | 14 |
| Architecture.Tests | 10 |

No live model key is used. Connector calls in these tests are in-memory fakes. Docker and Azure CLI were not run.

A1 checks: a spend entry at the cap refuses the live route and serves replay; equality trips the cap; yesterday's UTC spend does not; two identical prompts replay the same completion; every successful completion records a cost. The signing material in tests is the literal `test-key`.

A2 checks: signal shapes (step, ramp, oscillation, intermittent, unknown), scope selection, and one test per gate gap, config conflict, timestamp conflict, and grounding failure.

A3 checks: the default plan runs the ten read-only tools in order and builds stable evidence ids (UUID v5); `CreateCapa` is flagged and not executed; a tool failure returns no pack; the resolution agent rejects a function call, retries once on bad JSON, and emits schema-valid cited JSON. S1 ranks `engineeringChange` for ECO-4471 and names work order WO-19 as counter-evidence.

A4 checks, all in `tests/Orchestration.Tests`:

- S1 through the gateway in replay pauses at `AwaitingApproval` with category `EngineeringChange`, cause citing ECO-4471, and counter-evidence citing WO-19. A second incident with the same tool, chamber, parameter, and onset replays from the catalog. A throwing reasoner is not called.
- S2 (PLM showerhead rev C versus MES recipe id `showerhead` rev B, calibration lookup empty) escalates. The reason contains `rev C`, `rev B`, and `calibration records missing`. Hypotheses stay empty. The resolution agent is not called. A second replay run also leaves the throwing reasoner uncalled.
- `GetStateAsync` on a new workflow object that shares the session store returns the same recommendation.
- Approve, reject, and request-info resume on a new workflow object that shares the session store and the same `CheckpointManager`. Approve consumes the approval token. Reject keeps the hypothesis. Request-info clears hypotheses and stores `request_info: ` plus the comment.
- Severity `Critical` still gathers evidence before approval.
- A missing incident and a failed tool end at `Failed` with no hypothesis and no resolution call.
- An unknown evidence id from the resolution agent escalates with `grounding:` and publishes no hypothesis.
- `LangGraphWorkflowAdapter` throws and names `docs/langgraph-compat.md`.

## Behavior Bob should expect

The gateway is the only type that talks to a model. Agents call `IChatClient`. Orchestration calls the agents and does not reference `Mps.ModelGateway`. Live calls are serialized so two callers cannot both pass the cap. At or above the daily cap (default $2, UTC day) the gateway refuses the live route and serves replay. A replay miss falls through to the injected `IMockReasoner`. `QualityMockReasoner` in `Mps.Agents` owns the demo ranking. The gateway itself has no root-cause rules.

Azure OpenAI uses the OpenAI .NET SDK against the resource `/openai/v1/` endpoint. `Azure.AI.OpenAI` 2.1.0 was not added. It conflicts with `Microsoft.Extensions.AI.OpenAI` 10.11.0.

The investigation agent may call only the allowlisted read tools, at most 10 of them, inside 60 seconds. The evidence pack is built from tool results. Evidence ids are UUID v5 from the source and the external reference. The resolution agent has no tools and at most two model calls. A function call is rejected. Invalid JSON gets one retry, then an escalation.

`MafInvestigationWorkflow` builds a three-node Microsoft Agent Framework graph: `investigation-gate`, request port `human-approval`, and `approval-step`. Package `Microsoft.Agents.AI.Workflows` 1.25.0. The public host API is `InProcessExecution.Default.WithCheckpointing`, `RunAsync` until the first halt, and `ResumeAsync` from the stored `CheckpointInfo`. Graph messages are investigation id strings. Evidence and hypotheses live in `InvestigationSessionStore`. Resume needs that store and the same `CheckpointManager` instance. Both are in memory.

The gate runs gather, then `EvidenceGate`, and only then the resolution agent. `GroundingValidator` runs before a `Hypothesis` is stored. Insufficient evidence becomes stage `Escalated` with a reason that starts with `request_info: `. Contradictory evidence becomes `Escalated` with the conflict named. Those paths do not call the resolution agent. A grounded recommendation pauses at `AwaitingApproval`.

Approve hashes `CapaAction.ComputeActionHash(incidentId, proposedSummary)` and consumes the approval token, then sets `Approved`. `ProposedSummary` joins the claim's actions with `"; "` and falls back to the cause when there are no actions. `ProposedActionSummary` returns the stored string so a caller can issue a token for that same text. Reject sets `Rejected` and keeps the hypothesis. Request-info sets `Escalated`, prefixes the comment, and clears hypotheses. A blank comment uses `request_info: reviewer requested more information`. Resume is rejected unless the stage is `AwaitingApproval`.

This workflow does not call `IQmsCapaWriter` or `ITicketingPort`. Opening the CAPA stays with GB-3.

`LangGraphWorkflowAdapter` still throws. The mapping from the framework's workflow, executor, checkpoint, and request port onto LangGraph is in `docs/langgraph-compat.md`.

## Shared files other lanes can collide on

- `Directory.Packages.props`: `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` 10.11.0 (A1); `Microsoft.Agents.AI.Workflows` 1.25.0 (A4). `Azure.AI.OpenAI` is intentionally absent.
- `MPS.sln`: `ModelGateway.Tests`, `Agents.Tests`, `Orchestration.Tests`.
- `src/Mps.ModelGateway/Mps.ModelGateway.csproj`, `src/Mps.Agents/Mps.Agents.csproj`, `src/Mps.Orchestration/Mps.Orchestration.csproj`.
- `tests/Architecture.Tests/LayeringTests.cs`: one line in A1. The gateway check is `AssertDoesNotDependOn(ModelGateway, AllExcept("Mps.ModelGateway"))`. NetArchTest treats a namespace prefix as a dependency, so the assembly has to be allowed to see its own types.

## Open questions for Bob

1. `InvestigationStage` has no `RequestInfo` value. A request for more evidence is stored as `Escalated` with the reason prefix `request_info: `. This lane did not change the contract. Say if you want a real stage later.
2. Frozen `Mps.Application` has no incident read port. `IIncidentSource` is in `Mps.Agents.Incidents`. The API host can implement it. This lane did not add it to Application.
3. The lane brief names ADR-002. The tree has `docs/adr/ADR-001-contracts.md` only. This lane followed ADR-001.
4. Impact features, the evidence gate, and grounding live in `Mps.Agents` so the domain contract stays frozen. `ImpactScope` on the domain stays the smaller shape. The calculator wins over `IImpactScopeTool` unless the tool reports a broader lot or recipe whose key is already present on a local observation.
5. S2 treats the MES recipe id as the part name on a config claim, so recipe `showerhead` rev B conflicts with PLM part `showerhead` rev C. Say if a MES revision should be a different claim type.
6. The cost ledger and the replay catalog are in memory. The gateway records cost against `ChatOptions.ConversationId`, which is not the domain correlation id. The investigation snapshot still carries the incident correlation id. SQL can wait for the host lane.
7. Checkpoints are in memory. A new workflow object can resume only while the process still holds that `CheckpointManager`. A durable manager can replace `WithCheckpointing` later without changing `IInvestigationWorkflow`.
8. `docs/langgraph-compat.md` is in this lane because A4 asks for the mapping. K2 still owns the README and the rest of the doc set.
9. The logo at `design/brand/logo-concept-1.svg` was not touched.
