# ADR-014: The human decision sits on the Investigation page

Status: accepted, 2026-10-16

## Context
After a live run (an injected chamber-pressure oscillation on D-01-A), the owner found the Approval tab confusing:
- it opened with internal remarks about write authority;
- an Enforcement table (No approver name 400, used or expired approval rejected) read as if he had rejected something;
- the decision lived on a separate tab from the evidence it depends on;
- the AI summary called the oscillation "a gradual chamber pressure rise".

## Decision
- The decision (Approve CAPA + containment, Request more evidence, Override with justification, Reject, with the note
  and approver fields) is a **Decision** section on the Investigation page, directly under the evidence gate result.
  A "Go to decision" button jumps to it. Buttons use the standard colors (green approve, blue request, red reject and
  override).
- The Approval tab becomes **Decisions**: a list of investigations to decide. An old `/approvals/:id` link opens the
  investigation at its Decision section.
- Screens carry no architecture or enforcement remarks. Approval enforcement (approver required, single-use,
  expiring, hash-bound tokens; no agent write tools) stays in code, tests and SPEC §7, not on screen.
- Agent steps open and close from the whole row, with a chevron on the left, instead of a "Details" link.
- The evidence pack states the signal shape read from the data since the first rule break (oscillation, step,
  gradual drift, intermittent spikes), and the resolution prompt (1.5.2) must use it. An oscillation or a step is
  never described as a gradual rise. Eval case OSC-01 and unit tests cover it.
- Supplier and material words in AI text (silane, supplier notice, material lot, certificate of analysis, precursor)
  link to the glossary.

## Consequences
One page holds evidence, causes and the decision. The enforcement behavior is unchanged and still tested.
