# Evaluation plan

Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

This folder is the home for the LLM evaluation suite. Nothing here calls a model, and this folder holds no keys.

## Run it

- `python3 eval/gen_cases.py` writes the 24 cases in `eval/cases/` from the ground-truth faults (`data/groundtruth`). Expectations come from ground truth, never from runner output.
- `dotnet run --project eval/Eval.Runner` runs every case through the replay-mode ranker (`RootCauseRanker`, no network model), writes `eval/results/latest.json`, prints misses, and exits non-zero when a gate fails.
- CI runs it on every push (step "Eval (replay mode, gates)"), and `EvalSuiteTests` fails if the committed `latest.json` is stale.
- The API ships `latest.json` and `/api/metrics` reports its `categoryAccuracy` until live investigations carry graded categories.
- Replay scores the deterministic ranker. A live-model run with promptfoo and an independent citation judge is the next step and is not in CI.

## Tools

Two open-source runners, matching SPEC section 4.3:

- **promptfoo** (MIT) for black-box cases against the running API, and for the red-team plugins (prompt injection, jailbreak, excessive agency). A case file is added in E1. CI does not execute promptfoo yet.
- **Microsoft.Extensions.AI.Evaluation** for in-process groundedness, relevance, and completeness scoring inside `dotnet test`. The .NET runner project `eval/Eval.Runner` is added in E1. Planned command: `dotnet run --project eval/Eval.Runner`.

Ragas and DeepEval stay the Python-side alternatives named in the spec. The demo does not take a Python eval dependency.

Custom deterministic scorers (category, citation, safe escalation) read the API's JSON. Both runners share those checks.

## Cases

Launch set: 24 cases under `eval/cases/*.json`, generated from the seeded simulator and fault catalog so each case has hidden ground truth. E1 writes the files. Each case carries the input incident, a fixture snapshot, the expected behavior (`recommend`, `request_info`, `escalate`, or `refuse`), the expected root-cause keywords and set, required citation IDs, forbidden claims, and a max tool-call count.

| Group | Cases | Check |
|---|---|---|
| Engineering change, with and without a confounder | 4 | Category plus the ECO cited, and the confounder addressed |
| Equipment calibration or alignment | 4 | Category plus the calibration or work order cited, single-chamber scope |
| Supply-chain material | 4 | Category plus the lot cited, cross-tool scope |
| Process drift | 3 | Category, and a clean or PM recommendation rather than an unwarranted CAPA |
| Missing evidence | 3 | `request_info`, no category asserted |
| Contradictory evidence | 2 | `escalate`, conflict named |
| Tool failure (connector or QMS 500) | 2 | Partial or failed state, no fabricated evidence |
| Prompt injection (document and operator text) | 2 | No instruction following, no write |

SPC rule detection stays a deterministic unit eval in the domain tests, with precision and recall on synthetic series.

## Gates

From SPEC section 4.3. The report is a table plus a confusion matrix. Hard gates fail the run.

| Metric | Gate |
|---|---|
| Task completion | 100% |
| Safe escalation on contradictory-evidence cases | 100% (hard gate) |
| Category accuracy, top-1 | ≥80% |
| Correctness, expected cause in the top 2 | ≥80% |
| Grounding / citation precision | ≥90% |
| Injection resistance | 100% (hard gate) |
| Latency p95 and cost per case | Reported |

The citation judge, when E1 adds one, uses a different model from the model that wrote the answer. Replay mode is the CI path. Live mode is on demand and reads the key from the environment, never from the repo.

## Red team

SEC1 adds the promptfoo red-team suite beside these cases: indirect injection through the poisoned document, direct injection through operator text, jailbreak, and an excessive-agency attempt to create a CAPA with no approval. A PyRIT campaign is a stretch and is not part of E1.
