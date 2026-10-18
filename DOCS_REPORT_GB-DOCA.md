# DOCS_REPORT GB-DOCA

Lane DOC-A. Checked against the code at HEAD `0a3375f` on 2026-10-18. Docs only. No code, tests, values, keys, or schemas changed. `data/glossary.yaml` was not edited. `docs/adr/`, `docs/lane-reports/`, and `docs/design-questions/` were not touched.

## Files changed

- `docs/SPEC.md`
- `docs/architecture/event-envelope.md`
- `docs/diagrams/logical.md`
- `docs/diagrams/physical.md`
- `docs/diagrams/investigation-flow.md`
- `docs/diagrams/to-be-flow.md`
- `docs/diagrams/cicd.md`
- `docs/diagrams/README.md`
- `docs/diagrams/print/mermaid/logical.mmd`
- `docs/diagrams/print/mermaid/physical.mmd`
- `docs/diagrams/print/mermaid/investigation-flow.mmd`
- `docs/diagrams/print/mermaid/to-be-flow.mmd`
- `docs/diagrams/print/mermaid/cicd.mmd`
- `docs/diagrams/v2/model.py` (labels and notes only)
- `docs/diagrams/v2/README.md`

`docs/diagrams/as-is-flow.md` and `as-is-flow.mmd` were not in the edit list. The mermaid fence still matches the `.mmd`, and mermaid-cli rendered it. They were left as they were.

## Stale statements removed

Current-behavior text no longer says:

- The spec is an unbuilt draft, or that the hosted model is `gpt-4o-mini` plus `gpt-4o`.
- The console has a Replay/Live (or live / replay / mock) control for the model, a MOCK MODE label, a REPLAY MODE banner, or a LIVE AI badge. Plant-data Live and Replay remain. The model control is the read-only mode chip.
- The eval suite has 24 cases (or 27 / 28 as the current count), writes `eval/reports`, or has an `/eval` page. It is 29 cases in `eval/cases`, including SC8-01. The runner writes `eval/results/latest.json` and `latest.md`. Metrics are at `/harness/metrics`. `LiveModelEvalTests` runs only with `MPS_LIVE_EVAL=1`.
- Missing-evidence cases assert no category via `request_info`. Those cases escalate.
- Process drift has no discrete event. A recipe setpoint edit with no ECO is process drift.
- Supplier notices are a single lookup with material lots. Notices are only for material lots on that tool.
- CauseGuard treats a mention of a ruled-out record the same as a citation. A mention that dismisses a rival is allowed. Escalation is for a citation in `evidenceIds` or naming that record as the cause of its own class. The resolution prompt is 1.12.0. The investigation prompt is 1.1.0.
- A rejected model answer is discarded. It is stored as `rejectedAnswer` and shown as "What the AI proposed (not accepted)". Linked records keep one entry per investigation.
- The hosted app scales 0–3, uses serverless Azure SQL, a separate Static Web App, scale-to-zero, a $10/week budget, or a 7-day seed of 466,662 readings. It is one Container Apps replica (0.25 vCPU, 0.5 GiB), image `mcr.microsoft.com/dotnet/aspnet:10.0`, SQLite in the container, region `eastus2`, deployment `gpt-5.4-mini`. Each sensor keeps 600 readings. A reset does not seed a week. The prime stays in memory and is not published on the bus.
- Deploy runs on push or merge, runs Bicep what-if, requires a reviewer environment named `demo`, or smokes a LIVE badge. Deploy is manual `workflow_dispatch`, fork-safe OIDC, environment `azure-demo`. Smoke is health 200, `/api/tools` 401, and `/` redirecting to `/landing.html`. A deploy wipes the demo SQLite file.
- CI fails below 70% coverage, runs CodeQL on JavaScript inside `ci.yml`, or treats promptfoo and ZAP as pull-request jobs. Jobs are build, web, slow tests, bicep (compile only), gitleaks, dependency review (pull requests), and `codeql.yml` job `analyze` (C# only). Replay eval runs inside `build`. Coverage percent is not a failing gate.
- Auth is Entra for every run, with a built-in admin. Local mode is the default (no sign-in). Entra mode requires `ADMIN_EMAILS` or the app does not start.
- The platform line was a stack wish list (Mock LLM, Ollama as wired, Azure SQL as the demo store). It now names net10.0, SDK 10.0.100, Microsoft.Agents.AI 1.25.0, Microsoft.Extensions.AI 10.11.0, EF Core Sqlite 10.0.13, React 19, TypeScript 5.9, Vite 7, Recharts 2.15, SignalR 8, Node 22, and Vitest 3.

Section 7.4 lists the thirteen `tests/*` projects and the web Vitest suite. It does not count test methods. Playwright (`web/e2e`) is not part of the CI `web` job.

Section 8 (build plan) was kept as history, including the old Z1 line that deploy runs from `main`. Current deploy behavior is in §2.9, §7.5, `cicd.md`, and `physical.md`.

Section 10 kept the older decisions. Lines that would mislead now say `(superseded: ...)`, including the 27-case and 28-case counts and resolution prompts 1.6.0, 1.8.0, 1.9.0, 1.10.0, and 1.11.0. New 2026-10-18 entries cover model mode, sign-in and deploy, the telemetry window, CauseGuard / supplier notices / the rejected answer, and the harness banner. The existing 2026-10-18 entries for recipe-step limits, one linked record per investigation, and the fixed-width axis were left in place. Open question 1 (quota for `gpt-4o-mini` and `gpt-4o`) is marked closed. Questions 2–4 are unchanged.

## Render

`docs/diagrams/render.ps1` was not run. Its script writes `docs/diagrams/mps-sites.svg` and deck placeholders, which this lane does not own, and it rebuilds the ARCH D sheets from `docs/diagrams/sheets.mjs`, which this lane does not own. That sheet file still says CodeQL covers C# and JavaScript. Committed `docs/diagrams/print/*.svg` and `*.pdf`, and the v2 png/svg/pdf/drawio files, were left as they were. v2 drawing binaries are produced by `docs/diagrams/v2/build.sh`, not by `render.ps1`. `model.py` was checked with `python -m py_compile` only.

Mermaid was validated offline with `npx -y @mermaid-js/mermaid-cli` 12.0.0, Edge, and `docs/diagrams/mermaid.config.json`. Each print `.mmd` (logical, physical, cicd, investigation-flow, to-be-flow, as-is-flow) and both mermaid fences in `docs/SPEC.md` rendered to a temp folder. The temp output was deleted and was not committed. Each edited `.mmd` matches the first mermaid fence in its `.md`.

## GLOSSARY NEEDED

Another agent owns `data/glossary.yaml`. New all-caps tokens in `docs/SPEC.md` that are not already glossary terms:

- `LOCATION` — deploy variable named in §2.9.
- `TESTING` — token from the path `docs/TESTING.md` in §7.1.

`SDK` was added in the same pass and is already a glossary term. `OIDC` and `ECO` were already in the spec and in the glossary. `TFM` was not introduced. Setting names with underscores (`MODEL_MODE`, `ADMIN_EMAILS`, `MPS_LIVE_EVAL`) do not match the glossary scanner's acronym pattern.

## Not done

No git command that contacts GitHub. No Azure CLI. No glossary edit.
