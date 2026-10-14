# MPS Quality Agents

**Status: planning. No application code yet.**

This repo is a **StatusNeo proposal demo** for a fictional customer, **Meridian Precision Systems (MPS)**, a semiconductor process-equipment maker (thin-film deposition, plasma etch, photoresist strip, wafer clean). The demo is an agentic **Manufacturing Quality & Engineering Issue Resolution** workflow. A simulated tool fleet streams sensor telemetry, and SPC (Western Electric) alerts open tickets. An Investigation Agent and a Resolution Agent work out the root cause from grounded evidence in mock MES/QMS/PLM/ERP/maintenance systems and documents. A CAPA is created **only after explicit human approval**, with full traceability, evaluation and cost tracking.

> **Built openly with AI.** The code will be written by AI coding agents (Grok Build) working in parallel lanes, under human architecture, code review, CI, test and evaluation gates. Progress is tracked on the GitHub Project board and in issues.

All companies, equipment, people and data in this repo are **fictional and synthetic**.

## Plan
- [docs/SPEC.md](docs/SPEC.md): design specification (POV, demo, architecture, code, tests, eval)
- [docs/PLAN.md](docs/PLAN.md): execution plan (modules, lanes/agents, phases and gates, test strategy, eval and security, synthetic data, deck)

## Planned stack
.NET 8 / C#, Microsoft Agent Framework (with a LangGraph-compatible adapter), React + TypeScript, Azure (Container Apps, Azure OpenAI, Entra ID, Key Vault, App Insights), promptfoo + Microsoft.Extensions.AI.Evaluation, Stryker.NET, OWASP ZAP.

## Run locally (once code lands)
```
git clone https://github.com/ccc-bcweis/mps-quality-agents && cd mps-quality-agents
cp .env.example .env      # REPLAY mode by default; add a model key for LIVE mode
docker compose up         # or open in VS Code and press F5
```

## Ownership
All intellectual property in this build (design, specification, plan, code, data, documents and presentation materials) is owned by **Bryan C. Weis / Common Consulting Company**. It is a fictional demonstration built with AI assistance. All companies, equipment and data in it are fictional and synthetic. It is released under the MIT License (see LICENSE). The license grants usage rights but doesn't transfer ownership.

## License
MIT
