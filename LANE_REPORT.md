# Lane report: GB-4 UI

Branch `lane/4-ui`. Local commits only. Nothing was pushed. Contract files were not changed. `design/` is an untracked read-only copy from the design worktree and is not in these commits.

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company. MIT.

| Issue | Commit | What landed |
|---|---|---|
| U1 (#25) | `9efc8de` | React 19 + TypeScript + Vite app in `web/`. Tailwind theme from `design/brand/tailwind.theme.js` when that file is present, otherwise the same theme in `web/brand/tailwind.theme.cjs`. Logo `web/public/logo-concept-1.svg` (concept 1). Shell, fleet overview (eight tools, D-01 ALARM, E-02 WATCH), tool detail with SPC charts. CI web job runs `npm ci`, lint, test, and build. |
| U2 (#26) | `a9639e4` | Alerts, tickets, investigation (evidence, hypotheses, confounders, rules/AI/human trace), lifecycle bar driven by stage. Escalation stays on Investigation and is labeled. |
| U3 (#27) | `cafe682` | Approval dialog: type APPROVE, approver id, confirm ("Issue token and create CAPA"), reject with a reason, cancel posts nothing. CAPA screen keeps the decision result and shows the frozen advance error. |
| U4 (#28) | `e5ab6e7` | Fault injector, including preset S4 (two posts). Metrics screen and ROI screen. The replay/live banner and spend meter are in the shell from U1 because every page renders them; this commit adds the injector, metrics, and ROI behavior. |

## How it was verified

Before each commit:

- `npm run lint`, `npm test`, and `npm run build` in `web/` (TypeScript `noEmit`, Vite production build).
- `dotnet format MPS.sln --verify-no-changes`
- `dotnet build MPS.sln --configuration Release` (0 warnings, 0 errors)
- `dotnet test MPS.sln --configuration Release --no-build` (Domain.Tests 19 passed, Architecture.Tests 10 passed)

Final web suite: 17 files, 33 tests. That includes the approval dialog (disabled until the exact word APPROVE, cancel does not show the token, reject does not show the token, advance shows "Illegal CAPA transition."), the injector (new `corr-inject-` alert, S4 has two correlations), the mode banner (REPLAY MODE, `$0.00 of $2.00`, preview says the gateway is still replay), fleet, tool SPC, alerts, tickets, investigation S1/S2, CAPA empty state, metrics blanks, and the ROI figures `$96M` and `$7,599,195`.

No browser tools are available in this session. A person has not clicked through the dev server. Vite's production build emits a chunk-size warning (the recharts bundle is about 746 kB). Lint, tests, and the build still exit 0.

## Notes for Bob

- Demo list routes in `web/src/mocks/handlers.ts` sit in front of `contracts/msw/handlers.ts`. Health, metrics, decisions, and CAPA stay on the frozen handlers. S1 ids and hypothesis text match the contract fixture.
- `design/` was missing on this branch. The committed logo and `web/brand/tailwind.theme.cjs` let CI build before the design merge. When `design/brand/tailwind.theme.js` is present, Tailwind loads that file.
- Open contract questions are in `DESIGN_QUESTIONS.md`. None are blocking.
- PLAN §6.5 names `data/roi/` CSV. This lane stayed inside `web/` and computed the same inputs in `web/src/roi/model.ts`.
- The console does not call mock-enterprise. There is no force-QMS-500 button.
- Fleet p95 is not invented. Metrics nulls render as "—". The confusion sketch is labeled illustrative and is not an eval report.
- `docs/adr/ADR-002` is not in this checkout. The UI did not need it.
