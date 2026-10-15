# ADR-005: An escalated investigation cannot be approved straight into a CAPA

- Status: Accepted (Bob, architect, 2026-10-14)
- Context: In the first live run, the agents escalated because the evidence conflicted. The API still let a person approve, and approving opened a CAPA (corrective and preventive action) with no ranked cause behind it. That breaks the rule that every CAPA rests on cited evidence or on a named human judgment.

## Decision

When an investigation is **escalated**, plain approval is refused (HTTP 409). The approver has two options:

1. **Request more evidence** (`kind: requestInfo`). The escalated run is closed as superseded, and its reason records the note. The incident goes back to investigating, and the API starts a new agent run on the same incident with whatever data is now available. The response returns the new investigation. No CAPA is created.
2. **Override with justification** (`kind: override`, a contract v1.2 additive enum value). This needs an approver id and a written justification of at least 20 characters. It goes through the normal signed, single-use approval token. The justification is stored on the approval record as `Override of escalation by <approver>: <reason>`. It is also tagged on the `decision.override` trace span. That is the audit trail.

Override is refused unless the investigation is escalated. Reject is unchanged.

## Consequences

- The console shows an "escalation gate" panel on escalated cases. It has a note field and the two buttons. Approve stays disabled.
- Live AI cost: "Request more evidence" spends a second agent run. The gateway's $2/day cap still applies.
- Tests: Application.Tests (direct approve refused, override needs justification, override records why, override only when escalated, request more evidence supersedes). Web test for the gate. Contract test enum updated.
