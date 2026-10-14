# ADR-002: P0 architect decisions

Status: Accepted (Bob, architect) — 2026-10-14
Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

1. **Logo (final, chosen by Bryan Weis):** Meridian Precision Systems logo concept 1, "True Meridian" (`design/brand/logo-concept-1.svg`). Use it everywhere: UI shell, diagram title blocks, deck. Concepts 2 and 3 are retired (kept for history).
2. **StatusNeo mark:** keep the dashed text placeholder until Bryan approves official artwork.
3. **Name collision:** standalone "Meridian"/"MPS" overlap accepted; the full name "Meridian Precision Systems" is clear.
4. **ADR location:** all decisions live in `docs/adr/` (PLAN updated). No `docs/decisions/`.
5. **GitHub environment:** `azure-demo` (as in deploy.yml); supersedes `demo` in PLAN.
6. **Repo layout:** `docker-compose.yml` at root, Dockerfiles in `deploy/`, Bicep in `infra/` (supersedes SPEC `deploy/` for IaC).
7. **Budget:** Azure budget $40/month (Azure has no weekly budgets) + app-level $2/day model spend cap.
8. **FluentAssertions pinned to 7.x** (Apache-2.0); 8.x is commercial and excluded.
9. **Bicep what-if** runs only in the gated deploy workflow (Z1), not in PR CI; PR CI compiles Bicep.
10. **Merge policy:** main is protected with required checks (build, bicep, gitleaks, dependency review, analyze, web). Bob is sole merge approver; there is no second GitHub account, so approvals are recorded as PR comments rather than required reviews.
