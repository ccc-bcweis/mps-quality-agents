# LangGraph compatibility

Meridian Precision Systems keeps one orchestration boundary: `IInvestigationWorkflow`.

- `StartAsync(incidentId)` begins an investigation.
- `ResumeWithHumanInputAsync(investigationId, decision)` continues after a person approves, rejects, or asks for more information.
- `GetStateAsync(investigationId)` returns the current snapshot.

`MafInvestigationWorkflow` is the Microsoft Agent Framework implementation. Domain rules, the evidence gate, the grounding check, connectors, and evaluation do not reference the framework. A Python LangGraph service can implement the same three methods. `LangGraphWorkflowAdapter` is the seam for that service. It throws. It is not a second runtime and it does not call a network.

The read-only tools stay behind `Mps.Application.Tools`. An MCP server that exposes those tools is a later stretch. It is not in this lane.

Checkpoint payloads live in the in-memory `CheckpointManager` passed to the workflow. A new workflow object can resume when it shares that manager and the `InvestigationSessionStore`. The graph messages are investigation id strings. Evidence, hypotheses, and the proposed action summary live in the session store.

| Microsoft Agent Framework | LangGraph | This demo |
|---|---|---|
| Workflow (graph of executors) | `StateGraph` | Investigation flow: gate, human-approval port, approval step |
| Executor | Node | `investigation-gate` runs the investigation agent, evidence gate, resolution agent, and grounding validator. `approval-step` applies the human decision. |
| Edge | `add_edge` | Gate to the approval port, then the port to the approval step. A terminal gate yields output and does not enter the port. |
| Shared workflow state | Typed state plus reducers | `InvestigationSession` holds the evidence pack, hypotheses, stage, and proposed action summary. |
| Checkpointing | Checkpointer (`SqliteSaver` / `PostgresSaver`) | `CheckpointManager.CreateInMemory()` for the demo. Resume uses the checkpoint id from the approval halt. |
| Request port | `interrupt()` and `Command(resume=…)` | `RequestPort<string, string>` id `human-approval`. The host sends the investigation id back with `ExternalRequest.CreateResponse`. |
| `ChatClientAgent` and `AIFunction` | Agent node and tool node | The investigation agent calls read-only tools. The resolution agent has no tools. The workflow calls the agents. The agents call the model gateway. |
| Workflow events | Stream events, LangSmith, or OpenTelemetry | `SuperStepCompletedEvent` carries `CheckpointInfo`. `RequestInfoEvent` is the approval halt. `WorkflowOutputEvent` ends a terminal path. |

Insufficient or contradictory evidence escalates inside the gate executor. That path yields output and does not ask the resolution agent for a category. Approval verifies the approval token against `CapaAction.ComputeActionHash` and marks the investigation approved. Opening the CAPA stays on the write ports, which this workflow does not call.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
